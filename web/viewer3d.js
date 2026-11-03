// The 3D view: a ship, vehicle or weapon out of the game's own files, drawn
// with its stock loadout in place, its paints, and every fitted part
// clickable.
//
// Loaded as a module on first use (app.js adds the script tag), so nobody who
// never opens a model pays for three.js. Everything it draws comes from the
// local server's /api/models routes, which read the install:
//   scene    - what the entity is: model, palettes, paints, loadout tree
//   mesh     - a model packed as QWM1 (see ModelMesh.cs): header + buffers
//   material - a material file's sub-materials, layers resolved
//   texture  - a DDS reassembled from the archive's split mip chain, left
//              block-compressed for the GPU to take as it is
//
// The game draws hulls with layered materials: a tileable panel texture,
// tinted by one of the three palette colours, with a normal map and a gloss
// map beside it. That is reproduced here closely enough to read a livery, not
// pixel for pixel - wear, dirt, decals' stencils and the parallax are the
// engine's own and are left out.

import * as THREE from './vendor/three/three.module.min.js';
import { OrbitControls } from './vendor/three/addons/controls/OrbitControls.js';
import { RoomEnvironment } from './vendor/three/addons/environments/RoomEnvironment.js';
import { EffectComposer } from './vendor/three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from './vendor/three/addons/postprocessing/RenderPass.js';
import { GTAOPass } from './vendor/three/addons/postprocessing/GTAOPass.js';
import { OutputPass } from './vendor/three/addons/postprocessing/OutputPass.js';
import { UnrealBloomPass } from './vendor/three/addons/postprocessing/UnrealBloomPass.js';

const t = (s, v) => (typeof window.tx === 'function' ? window.tx(s, v) : s.replace(/\{(\w+)\}/g, (_, k) => v?.[k] ?? ''));
const escHtml = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

// Kinds of fitted part the side panel groups by, in the order a pilot reads a
// loadout. The type is the item's attach type, "WeaponGun/Gun".
const PART_GROUPS = [
  ['WeaponGun', 'Guns'],
  ['WeaponMining', 'Mining heads'],
  ['TractorBeam', 'Tractor beams'],
  ['Turret', 'Turrets & mounts'],
  ['TurretBase', 'Turrets & mounts'],
  ['MissileLauncher', 'Missile racks'],
  ['BombLauncher', 'Missile racks'],
  ['Missile', 'Missiles'],
  ['Bomb', 'Missiles'],
  ['WeaponDefensive', 'Countermeasures'],
  ['MainThruster', 'Thrusters'],
  ['ManneuverThruster', 'Thrusters'],
  ['WeaponAttachment', 'Attachments'],
  ['Char_Armor_Helmet', 'Armour pieces'],
  ['Char_Armor_Torso', 'Armour pieces'],
  ['Char_Armor_Arms', 'Armour pieces'],
  ['Char_Armor_Legs', 'Armour pieces'],
  ['Char_Armor_Backpack', 'Armour pieces'],
];

function groupOf(type) {
  const kind = String(type || '').split('/')[0];
  return PART_GROUPS.find(([k]) => k === kind)?.[1] || 'Other parts';
}

// --- data ---------------------------------------------------------------

const meshCache = new Map();
const materialCache = new Map();
const textureCache = new Map();
const textureLog = { ok: 0, failed: [], kinds: {} };

async function getJson(url) {
  const res = await fetch(url);
  if (!res.ok) {
    let message = `${res.status}`;
    try { message = (await res.json()).message || message; } catch { /* not JSON */ }
    throw new Error(message);
  }
  return res.json();
}

function loadMesh(path, stamp) {
  const key = path.toLowerCase();
  if (!meshCache.has(key)) {
    meshCache.set(key, fetch(`/api/models/mesh?path=${encodeURIComponent(path)}&v=${stamp}`)
      .then(r => (r.ok ? r.arrayBuffer() : null))
      .then(buf => (buf ? parseMesh(buf) : null))
      .catch(() => null));
  }
  return meshCache.get(key);
}

function loadMaterial(path, stamp) {
  const key = path.toLowerCase();
  if (!materialCache.has(key)) {
    materialCache.set(key, getJson(`/api/models/material?path=${encodeURIComponent(path)}&v=${stamp}`).catch(() => null));
  }
  return materialCache.get(key);
}

/** QWM1: magic, header length, JSON header, positions, normals, uvs, indices. */
export function parseMesh(buf) {
  const view = new DataView(buf);
  const magic = String.fromCharCode(view.getUint8(0), view.getUint8(1), view.getUint8(2), view.getUint8(3));
  if (magic !== 'QWM1') return null;
  const headerLength = view.getUint32(4, true);
  const header = JSON.parse(new TextDecoder().decode(new Uint8Array(buf, 8, headerLength)));
  let at = 8 + headerLength;
  const n = header.vertices;
  const positions = new Float32Array(buf, at, n * 3); at += n * 12;
  const normals = new Int8Array(buf, at, n * 4); at += n * 4;
  const uvs = new Float32Array(buf, at, n * 2); at += n * 8;
  const indices = new Uint32Array(buf, at, header.indices);
  return { header, positions, normals, uvs, indices };
}

// --- textures -------------------------------------------------------------

/** A DDS as the archive holds it, uploaded without decoding. Null when the GPU cannot take the format. */
export function parseDds(buf, caps) {
  const view = new DataView(buf);
  if (view.getUint32(0, true) !== 0x20534444) return null;
  const height = view.getUint32(12, true);
  const width = view.getUint32(16, true);
  const mips = Math.max(1, view.getUint32(28, true));
  const fourCC = String.fromCharCode(view.getUint8(84), view.getUint8(85), view.getUint8(86), view.getUint8(87));
  let offset = 128;
  let format = null, block = 16, signed = false, srgb = false;
  if (fourCC === 'DX10') {
    const dxgi = view.getUint32(128, true);
    offset = 148;
    switch (dxgi) {
      case 70: case 71: case 72: format = 'dxt1'; block = 8; srgb = dxgi === 72; break;
      case 73: case 74: case 75: format = 'dxt3'; srgb = dxgi === 75; break;
      case 76: case 77: case 78: format = 'dxt5'; srgb = dxgi === 78; break;
      case 79: case 80: format = 'bc4'; block = 8; break;
      case 81: format = 'bc4'; block = 8; signed = true; break;
      case 82: case 83: format = 'bc5'; break;
      case 84: format = 'bc5'; signed = true; break;
      case 97: case 98: case 99: format = 'bc7'; srgb = dxgi === 99; break;
      default: return null;
    }
  } else {
    const map = { DXT1: ['dxt1', 8], DXT3: ['dxt3', 16], DXT5: ['dxt5', 16], ATI1: ['bc4', 8], BC4U: ['bc4', 8], ATI2: ['bc5', 16], BC5U: ['bc5', 16] };
    if (!map[fourCC]) return null;
    [format, block] = map[fourCC];
  }

  const formats = {
    dxt1: caps.s3tc && THREE.RGBA_S3TC_DXT1_Format,
    dxt3: caps.s3tc && THREE.RGBA_S3TC_DXT3_Format,
    dxt5: caps.s3tc && THREE.RGBA_S3TC_DXT5_Format,
    bc4: caps.rgtc && (signed ? THREE.SIGNED_RED_RGTC1_Format : THREE.RED_RGTC1_Format),
    bc5: caps.rgtc && (signed ? THREE.SIGNED_RED_GREEN_RGTC2_Format : THREE.RED_GREEN_RGTC2_Format),
    bc7: caps.bptc && THREE.RGBA_BPTC_Format,
  };
  const glFormat = formats[format];
  if (!glFormat) return null;

  const mipmaps = [];
  let w = width, h = height;
  for (let i = 0; i < mips; i++) {
    const size = Math.max(1, (w + 3) >> 2) * Math.max(1, (h + 3) >> 2) * block;
    if (offset + size > buf.byteLength) break;
    mipmaps.push({ data: new Uint8Array(buf, offset, size), width: w, height: h });
    offset += size;
    w = Math.max(1, w >> 1);
    h = Math.max(1, h >> 1);
  }
  if (!mipmaps.length) return null;
  return { mipmaps, width, height, format: glFormat, kind: format, signed, srgb };
}

function loadTexture(path, { gloss = false, colour = false, stamp, caps, max }) {
  const key = `${path.toLowerCase()}|${gloss}|${colour}|${max}`;
  if (!textureCache.has(key)) {
    const url = `/api/models/texture?path=${encodeURIComponent(path)}${gloss ? '&gloss=true' : ''}&max=${max}&v=${stamp}`;
    textureCache.set(key, fetch(url)
      .then(r => (r.ok ? r.arrayBuffer() : null))
      .then(buf => {
        const dds = buf && parseDds(buf, caps);
        if (!dds) {
          textureLog.failed.push(`${path}${gloss ? ' (gloss)' : ''}: ${buf ? 'format' : 'missing'}`);
          return null;
        }
        textureLog.ok += 1;
        const kindKey = dds.kind + (dds.signed ? "-signed" : "") + (colour ? "-colour" : "");
        textureLog.kinds[kindKey] = (textureLog.kinds[kindKey] || 0) + 1;
        const tex = new THREE.CompressedTexture(dds.mipmaps, dds.width, dds.height, dds.format);
        tex.wrapS = tex.wrapT = THREE.RepeatWrapping;
        tex.minFilter = dds.mipmaps.length > 1 ? THREE.LinearMipmapLinearFilter : THREE.LinearFilter;
        tex.magFilter = THREE.LinearFilter;
        tex.anisotropy = caps.anisotropy;
        tex.flipY = false;
        if (colour) tex.colorSpace = THREE.SRGBColorSpace;
        tex.userData.signed = dds.signed;
        tex.userData.kind = dds.kind;
        tex.needsUpdate = true;
        return tex;
      })
      .catch(() => null));
  }
  return textureCache.get(key);
}

function tiled(texture, tiling) {
  if (!texture) return null;
  if (!tiling || tiling === 1) return texture;
  const copy = texture.clone();
  copy.repeat.set(tiling, tiling);
  copy.needsUpdate = true;
  return copy;
}

function boundsOf(positions, index) {
  let x0 = Infinity, y0 = Infinity, z0 = Infinity, x1 = -Infinity, y1 = -Infinity, z1 = -Infinity;
  for (let i = 0; i < index.length; i++) {
    const v = index[i] * 3;
    const x = positions[v], y = positions[v + 1], z = positions[v + 2];
    if (x < x0) x0 = x; if (x > x1) x1 = x;
    if (y < y0) y0 = y; if (y > y1) y1 = y;
    if (z < z0) z0 = z; if (z > z1) z1 = z;
  }
  return index.length
    ? new THREE.Box3(new THREE.Vector3(x0, y0, z0), new THREE.Vector3(x1, y1, z1))
    : new THREE.Box3(new THREE.Vector3(), new THREE.Vector3());
}

// --- materials ------------------------------------------------------------

const srgb = rgb => new THREE.Color().setRGB(rgb[0] / 255, rgb[1] / 255, rgb[2] / 255, THREE.SRGBColorSpace);
const linear = rgb => new THREE.Color(rgb[0], rgb[1], rgb[2]);
const luminance = c => 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;

/**
 * The engine's specular colour is reflectance at head-on: about 0.04 for paint
 * and plastic, the metal's own colour for metal. Folded into the metalness
 * workflow three.js draws with.
 *
 * Except at white. A trim or atlas layer that reads its reflectance from a
 * specular map carries Specular="1,1,1" as the map's multiplier, not as a
 * colour - taken literally, every one of them is polished chrome, and the
 * Corsair's trim came out as white and black mirror patches. Those are drawn
 * as a worn, part-metal surface instead, with the colour map deciding the look.
 */
function metalFrom(diffuse, spec) {
  if (luminance(spec) > 0.9) return { base: diffuse.clone(), metal: 0.35 };
  const metal = THREE.MathUtils.smoothstep(luminance(spec), 0.1, 0.45);
  const base = diffuse.clone().lerp(spec, metal);
  return { base, metal };
}

/**
 * Normal maps are two-channel (BC5) and gloss sits in a map of its own: both
 * need a word with the shader. The uniforms live on the material so a map
 * that arrives later can change them without a recompile.
 */
function patchShader(material, glossScale) {
  const uniforms = {
    qwGloss: { value: glossScale },
    qwSigned: { value: 0 },
  };
  material.userData.uniforms = uniforms;
  material.onBeforeCompile = shader => {
    Object.assign(shader.uniforms, uniforms);
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', '#include <common>\nuniform float qwGloss;\nuniform float qwSigned;')
      .replace('#include <normal_fragment_maps>', THREE.ShaderChunk.normal_fragment_maps.replace(
        'vec3 mapN = texture2D( normalMap, vNormalMapUv ).xyz * 2.0 - 1.0;',
        'vec2 qwN = texture2D( normalMap, vNormalMapUv ).xy; qwN = qwSigned > 0.5 ? qwN : qwN * 2.0 - 1.0;\n'
        + '\tvec3 mapN = vec3( qwN, sqrt( max( 0.0, 1.0 - dot( qwN, qwN ) ) ) );'))
      .replace('#include <roughnessmap_fragment>',
        'float roughnessFactor = roughness;\n#ifdef USE_ROUGHNESSMAP\n'
        + '\tfloat qwG = texture2D( roughnessMap, vRoughnessMapUv ).r * qwGloss;\n'
        + '\troughnessFactor = clamp( 1.0 - qwG, 0.06, 1.0 );\n#endif');
  };
  material.customProgramCacheKey = () => 'qw-layer';
}

// Decals that only carry relief (parallax panel lines, rivets) draw as flat
// white sheets without the engine's parallax; they are left out.
const reliefDecal = name => /pom|parallax|height/i.test(name || '');
const glowing = sub => /glow|light|emissive|illum/i.test(sub.name || '') && luminance(linear(sub.emissive || [0, 0, 0])) > 0;

class MaterialBuilder {
  constructor(view) {
    this.view = view;
  }

  /** One three.js material for one sub-material, under one palette. Textures land later. */
  build(sub, palette) {
    const { view } = this;
    if (!sub) return view.fallback;

    if (sub.glass) {
      const tint = sub.tintsGlass && palette ? srgb(palette.glass) : linear(sub.diffuse);
      return this.inside(sub, new THREE.MeshPhysicalMaterial({
        color: tint.lerp(new THREE.Color(0.05, 0.06, 0.07), 0.55), metalness: 0, roughness: 0.04,
        transparent: true, opacity: 0.32, depthWrite: false, envMapIntensity: 1.6,
        clearcoat: 1, clearcoatRoughness: 0.03, side: THREE.DoubleSide,
      }));
    }

    if (/^LayerBlend/i.test(sub.shader || '') && sub.layers?.some(l => /^BaseLayer/i.test(l.name))) {
      return this.inside(sub, this.layerBlend(sub, palette));
    }

    const layer = sub.layers?.[0];
    let colour, spec, gloss, paletteDriven = false;
    if (layer && layer.paletteTint >= 1 && layer.paletteTint <= 3 && palette) {
      const entry = palette[['a', 'b', 'c'][layer.paletteTint - 1]];
      colour = srgb(entry.tint).multiply(linear(layer.tint));
      spec = srgb(entry.spec);
      gloss = entry.gloss / 255;
      paletteDriven = true;
    } else if (layer) {
      colour = linear(layer.tint).multiply(linear(layer.colour));
      spec = linear(layer.specular);
      gloss = layer.gloss ?? 0.5;
    } else {
      colour = linear(sub.diffuse);
      spec = linear(sub.specular);
      gloss = sub.gloss ?? 0.5;
    }
    const glossScale = gloss * (layer?.glossMult ?? 1);

    const { base, metal } = paletteDriven
      ? (() => { const m = THREE.MathUtils.smoothstep(luminance(spec), 0.1, 0.45); return { base: colour.clone().lerp(spec, m), metal: m }; })()
      : metalFrom(colour, spec);
    // Guns put their decal sheets on the plain Illum shader -
    // BEHR_LaserCannon_S4's graphic_decals and pom_decals - and drawn as
    // ordinary surfaces they covered the gun in white panels.
    const decal = sub.decal || /decal/i.test(sub.name || '');
    const glow = glowing(sub);
    const emissive = glow ? linear(sub.emissive) : new THREE.Color(0, 0, 0);
    // Painted panels are lacquered: a thin clear coat over the colour, which
    // is what makes a livery catch the light the way it does in the hangar.
    const lacquered = paletteDriven && metal < 0.5 && !decal && !glow;
    const Material = lacquered ? THREE.MeshPhysicalMaterial : THREE.MeshStandardMaterial;
    const material = new Material({
      ...(lacquered ? { clearcoat: 0.55, clearcoatRoughness: 0.14 } : {}),
      color: base,
      metalness: metal,
      // Until a gloss map lands, nothing is mirror-smooth: a value of 255 is
      // the map's multiplier, not the surface.
      roughness: THREE.MathUtils.clamp(1 - glossScale, 0.32, 1),
      emissive,
      emissiveIntensity: glow ? 2.0 : 0,
      transparent: decal || sub.opacity < 0.99,
      opacity: decal ? 1 : sub.opacity,
      alphaTest: sub.alphaTest > 0 ? sub.alphaTest : (decal ? 0.02 : 0),
      depthWrite: !decal,
      polygonOffset: decal,
      polygonOffsetFactor: decal ? -2 : 0,
      polygonOffsetUnits: decal ? -2 : 0,
    });
    material.userData.sub = sub.name;
    patchShader(material, glossScale);

    // A decal without its picture is a grey sheet floating over the hull.
    material.userData.decal = decal;
    if (decal) material.visible = false;
    this.inside(sub, material);
    if (decal && reliefDecal(sub.name)) return material;
    this.textures(material, sub, layer, decal, glow);
    return material;
  }

  /**
   * The cabin and cockpit are in the hull model too, on sub-materials named
   * Int_. The engine lights them with the ship's own lamps and shades them
   * with interior shaders this view does not have, so drawn here they come out
   * dark, flat and seen through every gap in the hull. They are hidden until
   * the Interior box is ticked.
   */
  inside(sub, material) {
    if (!/^int_/i.test(sub.name || '')) return material;
    material.userData.interior = true;
    material.userData.wanted = material.visible;
    material.visible = material.visible && this.view.interior;
    this.view.interiorMaterials.add(material);
    return material;
  }

  /**
   * LayerBlend: how armour, clothing and FPS weapons are painted. Up to four
   * base layers, each a tinted tileable surface, mixed per pixel by the
   * object's blend map - its red, green and blue lift the second, third and
   * fourth layer over the first - over the object's own normal map. The wear
   * layers and their mask are the engine's scuffing and are left out, as on
   * hulls. A layer the palette tints takes the palette's colour.
   */
  layerBlend(sub, palette) {
    const { view } = this;
    const base = sub.layers.filter(l => /^BaseLayer/i.test(l.name)).slice(0, 4);
    // Each layer is a surface of its own: a bare-metal layer is tinted black
    // with a metal's reflectance, and drawn as a dull black plastic it made
    // every gun look painted. Folded into colour and metalness per layer.
    const colourOf = l => {
      if (l.paletteTint >= 1 && l.paletteTint <= 3 && palette) {
        const entry = palette[['a', 'b', 'c'][l.paletteTint - 1]];
        const { base, metal } = metalFrom(srgb(entry.tint), srgb(entry.spec));
        return { colour: base, gloss: entry.gloss / 255, metal };
      }
      const { base, metal } = metalFrom(linear(l.tint).multiply(linear(l.colour)), linear(l.specular || [0.04, 0.04, 0.04]));
      return { colour: base, gloss: (l.gloss ?? 0.5) * (l.glossMult ?? 1), metal };
    };
    const looks = base.map(colourOf);
    while (looks.length < 4) looks.push(looks[looks.length - 1]);

    const material = new THREE.MeshStandardMaterial({
      color: looks[0].colour,
      metalness: looks[0].metal,
      roughness: 1,
    });
    material.userData.sub = sub.name;
    const uniforms = {
      qwGloss: { value: 1 },
      qwSigned: { value: 0 },
      qwBlend: { value: null },
      qwHasBlend: { value: 0 },
      qwL2: { value: looks[1].colour },
      qwL3: { value: looks[2].colour },
      qwL4: { value: looks[3].colour },
      qwGlosses: { value: new THREE.Vector4(looks[0].gloss, looks[1].gloss, looks[2].gloss, looks[3].gloss) },
      qwMetals: { value: new THREE.Vector4(looks[0].metal, looks[1].metal, looks[2].metal, looks[3].metal) },
    };
    material.userData.uniforms = uniforms;
    material.defines = { USE_UV: '' };
    material.onBeforeCompile = shader => {
      Object.assign(shader.uniforms, uniforms);
      shader.fragmentShader = shader.fragmentShader
        .replace('#include <common>', '#include <common>\nuniform float qwGloss;\nuniform float qwSigned;\nuniform sampler2D qwBlend;\nuniform float qwHasBlend;\nuniform vec3 qwL2;\nuniform vec3 qwL3;\nuniform vec3 qwL4;\nuniform vec4 qwGlosses;\nuniform vec4 qwMetals;')
        .replace('#include <color_fragment>', '#include <color_fragment>\n'
          + '\tvec4 qwB = qwHasBlend > 0.5 ? texture2D( qwBlend, vUv ) : vec4( 0.0 );\n'
          + '\tdiffuseColor.rgb = mix( mix( mix( diffuseColor.rgb, qwL2, qwB.r ), qwL3, qwB.g ), qwL4, qwB.b );\n'
          + '\tfloat qwG = mix( mix( mix( qwGlosses.x, qwGlosses.y, qwB.r ), qwGlosses.z, qwB.g ), qwGlosses.w, qwB.b );')
        .replace('#include <normal_fragment_maps>', THREE.ShaderChunk.normal_fragment_maps.replace(
          'vec3 mapN = texture2D( normalMap, vNormalMapUv ).xyz * 2.0 - 1.0;',
          'vec2 qwN = texture2D( normalMap, vNormalMapUv ).xy; qwN = qwSigned > 0.5 ? qwN : qwN * 2.0 - 1.0;\n'
          + '\tvec3 mapN = vec3( qwN, sqrt( max( 0.0, 1.0 - dot( qwN, qwN ) ) ) );'))
        .replace('#include <roughnessmap_fragment>', 'float roughnessFactor = clamp( 1.0 - qwG, 0.08, 1.0 );')
        .replace('#include <metalnessmap_fragment>', 'float metalnessFactor = mix( mix( mix( qwMetals.x, qwMetals.y, qwB.r ), qwMetals.z, qwB.g ), qwMetals.w, qwB.b );');
    };
    material.customProgramCacheKey = () => 'qw-layerblend';

    const opts = { stamp: view.stamp, caps: view.caps, max: view.textureMax };
    const pending = [];
    // The base layer's own surface: a tileable colour map that darkens and
    // breaks up its tint. Without it a gun's "dull metal" was a flat light
    // grey that read as white paint under studio light.
    const baseLayer = base[0];
    if (baseLayer?.diffuse) {
      pending.push(loadTexture(baseLayer.diffuse, { ...opts, colour: true }).then(tex => {
        if (!tex) return;
        material.map = tiled(tex, baseLayer.tiling || 1);
        material.needsUpdate = true;
      }));
    }
    if (baseLayer?.normal && !Object.values(sub.textures || {}).some(file => /_ddna?\./i.test(file))) {
      pending.push(loadTexture(baseLayer.normal, opts).then(tex => {
        if (!tex || tex.userData.kind !== 'bc5') return;
        material.normalMap = tiled(tex, baseLayer.tiling || 1);
        uniforms.qwSigned.value = tex.userData.signed ? 1 : 0;
        material.normalScale.set(1, -1);
        material.needsUpdate = true;
      }));
    }
    const blendPath = Object.entries(sub.textures || {}).find(([, file]) => /_blend\./i.test(file))?.[1];
    if (blendPath) {
      pending.push(loadTexture(blendPath, opts).then(tex => {
        if (!tex || tex.userData.kind === 'bc4' || tex.userData.kind === 'bc5') return;
        tex.wrapS = tex.wrapT = THREE.ClampToEdgeWrapping;
        uniforms.qwBlend.value = tex;
        uniforms.qwHasBlend.value = 1;
        material.needsUpdate = true;
      }));
    }
    const normalPath = Object.values(sub.textures || {}).find(file => /_ddna?\./i.test(file));
    if (normalPath) {
      pending.push(loadTexture(normalPath, opts).then(tex => {
        if (!tex || tex.userData.kind !== 'bc5') return;
        material.normalMap = tex;
        uniforms.qwSigned.value = tex.userData.signed ? 1 : 0;
        material.normalScale.set(1, -1);
        material.needsUpdate = true;
      }));
    }
    view.pending(pending);
    return material;
  }

  textures(material, sub, layer, decal, glow) {
    const { view } = this;
    const opts = { stamp: view.stamp, caps: view.caps, max: view.textureMax };
    const diffusePath = layer?.diffuse || sub.textures?.TexSlot1 || sub.textures?.Diffuse;
    const normalPath = layer?.normal || ((sub.textures?.TexSlot2 || '').toLowerCase().includes('ddn') ? sub.textures.TexSlot2 : null);
    const tiling = layer?.tiling || 1;
    const pending = [];

    if (diffusePath && !/_ddna?\./i.test(diffusePath)) {
      pending.push(loadTexture(diffusePath, { ...opts, colour: true }).then(tex => {
        if (!tex) return;
        // A layer's colour map is a tileable grey that the tint colours; an
        // object's own is the colour itself.
        material.map = tiled(tex, tiling);
        if (glow) material.emissiveMap = material.map;
        if (decal) {
          material.userData.wanted = true;
          material.visible = !material.userData.interior || view.interior;
          material.color.set(0xd8d8d8);
          material.metalness = 0;
          material.roughness = 0.55;
        }
        material.needsUpdate = true;
      }));
    }
    if (normalPath && !decal) {
      pending.push(loadTexture(normalPath, opts).then(tex => {
        if (!tex || tex.userData.kind !== 'bc5') return;
        material.normalMap = tiled(tex, tiling);
        material.userData.uniforms.qwSigned.value = tex.userData.signed ? 1 : 0;
        material.normalScale.set(1, -1);
        material.needsUpdate = true;
      }));
      pending.push(loadTexture(normalPath, { ...opts, gloss: true }).then(tex => {
        if (!tex || tex.userData.kind !== 'bc4') return;
        material.roughnessMap = tiled(tex, tiling);
        material.roughness = 1;
        material.needsUpdate = true;
      }));
    }
    view.pending(pending);
  }
}

/**
 * The dark field behind the model, as a texture: the page's own gradient was
 * behind a transparent canvas, and the effects chain draws an opaque one, so
 * with effects on the model sat on black. In the scene it survives, and a
 * saved picture carries it.
 */
function backdrop() {
  const canvas = document.createElement('canvas');
  canvas.width = 16;
  canvas.height = 512;
  const g = canvas.getContext('2d');
  const grad = g.createLinearGradient(0, 0, 0, 512);
  grad.addColorStop(0, '#0b1724');
  grad.addColorStop(0.55, '#10233a');
  grad.addColorStop(1, '#04080e');
  g.fillStyle = grad;
  g.fillRect(0, 0, 16, 512);
  const tex = new THREE.CanvasTexture(canvas);
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}

// --- the view -------------------------------------------------------------

class ModelView {
  constructor(host, opts) {
    this.host = host;
    this.opts = opts;
    this.disposed = false;
    this.parts = [];
    this.selected = null;
    this.textureMax = 2048;
    this.loadingCount = 0;
    this.loadedCount = 0;

    const canvas = host.querySelector('canvas');
    const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, powerPreference: 'high-performance', preserveDrawingBuffer: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.NeutralToneMapping;
    renderer.toneMappingExposure = 1.05;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer = renderer;

    const ext = n => !!renderer.extensions.get(n);
    this.caps = {
      s3tc: ext('WEBGL_compressed_texture_s3tc'),
      rgtc: ext('EXT_texture_compression_rgtc'),
      bptc: ext('EXT_texture_compression_bptc'),
      anisotropy: renderer.capabilities.getMaxAnisotropy?.() || 1,
    };
    ext('WEBGL_compressed_texture_s3tc_srgb');

    const scene = new THREE.Scene();
    this.scene = scene;
    scene.background = backdrop();
    const pmrem = new THREE.PMREMGenerator(renderer);
    scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
    scene.environmentIntensity = 0.85;
    pmrem.dispose();

    const camera = new THREE.PerspectiveCamera(35, 1, 0.05, 5000);
    this.camera = camera;
    const controls = new OrbitControls(camera, canvas);
    controls.addEventListener('change', () => this.invalidate());
    controls.enableDamping = true;
    controls.dampingFactor = 0.08;
    controls.screenSpacePanning = true;
    this.controls = controls;

    const key = new THREE.DirectionalLight(0xfff1e0, 2.6);
    key.castShadow = true;
    key.shadow.mapSize.set(4096, 4096);
    key.shadow.bias = -0.0004;
    key.shadow.normalBias = 0.02;
    this.key = key;
    const rim = new THREE.DirectionalLight(0x9fd8ff, 1.4);
    this.rim = rim;
    const fill = new THREE.DirectionalLight(0xffffff, 1.0);
    this.fill = fill;
    const sky = new THREE.HemisphereLight(0xbfd9ff, 0x1a1410, 0.35);
    this.sky = sky;
    scene.add(key, key.target, rim, fill, sky);

    const floor = new THREE.Mesh(new THREE.CircleGeometry(1, 96), new THREE.ShadowMaterial({ opacity: 0.38 }));
    floor.rotation.x = -Math.PI / 2;
    floor.receiveShadow = true;
    this.floor = floor;
    scene.add(floor);

    const grid = new THREE.PolarGridHelper(1, 12, 6, 96, 0x2c5a6e, 0x183442);
    grid.material.transparent = true;
    grid.material.opacity = 0.45;
    this.grid = grid;
    scene.add(grid);

    // CryEngine is Z-up with the nose along +Y; three.js is Y-up.
    const root = new THREE.Group();
    root.rotation.x = -Math.PI / 2;
    this.root = root;
    scene.add(root);

    this.fallback = new THREE.MeshStandardMaterial({ color: 0x8a9096, metalness: 0.4, roughness: 0.5 });
    this.interior = false;
    this.interiorMaterials = new Set();
    this.showSuit = true;
    this.materials = new MaterialBuilder(this);

    this.composer = null;
    this.quality = opts.quality ?? true;
    // Drawn only when something changed. Counted, so the saving can be
    // measured: renders, and the time spent issuing them.
    this.dirty = true;
    this.stats = { frames: 0, renders: 0, renderMs: 0 };
    this.resize = this.resize.bind(this);
    this.frame = this.frame.bind(this);
    window.addEventListener('resize', this.resize);
    this.resize();

    this.raycaster = new THREE.Raycaster();
    canvas.addEventListener('pointerdown', e => { this.downAt = [e.clientX, e.clientY]; });
    canvas.addEventListener('pointerup', e => {
      if (!this.downAt) return;
      const moved = Math.hypot(e.clientX - this.downAt[0], e.clientY - this.downAt[1]);
      this.downAt = null;
      if (moved < 5) this.pick(e);
    });

    requestAnimationFrame(this.frame);
  }

  pending(list) {
    this.loadingCount += list.length;
    this.progress();
    list.forEach(p => p.finally(() => { this.loadedCount += 1; this.progress(); this.invalidate(); }));
  }

  /** Something on screen changed: draw the next frame. */
  invalidate() {
    this.dirty = true;
  }

  progress() {
    const el = this.host.querySelector('[data-v3d-progress]');
    if (!el) return;
    const left = this.loadingCount - this.loadedCount;
    el.textContent = left > 0 ? t('Loading textures… {left} left', { left }) : '';
    el.hidden = left <= 0;
  }

  setStatus(text) {
    const el = this.host.querySelector('[data-v3d-status]');
    if (el) { el.textContent = text || ''; el.hidden = !text; }
  }

  resize() {
    this.invalidate();
    const box = this.host.querySelector('.v3d-stage').getBoundingClientRect();
    const w = Math.max(1, box.width), h = Math.max(1, box.height);
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    this.composer?.setSize(w, h);
  }

  /**
   * Lighting presets. The game lights a ship with its surroundings - a hangar's
   * lamps, a planet's sun - and none of that comes with the model, so the view
   * offers a few. Studio is bright and even, for reading a hull; Hangar is warm
   * and overhead; Deep space is one hard sun and a rim, as a ship looks in
   * flight.
   */
  setLighting(name) {
    this.invalidate();
    const presets = {
      studio: { env: 0.8, key: 2.6, rim: 1.3, fill: 0.8, sky: 0.4, exposure: 1.0, keyColour: 0xffffff, rimColour: 0xbfe6ff },
      hangar: { env: 0.85, key: 2.6, rim: 1.4, fill: 0.6, sky: 0.35, exposure: 1.05, keyColour: 0xfff1e0, rimColour: 0x9fd8ff },
      space: { env: 0.18, key: 5.5, rim: 2.2, fill: 0.15, sky: 0.05, exposure: 1.1, keyColour: 0xfff6ea, rimColour: 0x6fa8ff },
    };
    const p = presets[name] || presets.studio;
    this.lighting = name in presets ? name : 'studio';
    this.scene.environmentIntensity = p.env;
    this.key.intensity = p.key;
    this.key.color.set(p.keyColour);
    this.rim.intensity = p.rim;
    this.rim.color.set(p.rimColour);
    this.fill.intensity = p.fill;
    this.sky.intensity = p.sky;
    this.baseExposure = p.exposure;
    this.renderer.toneMappingExposure = p.exposure * (this.brightness ?? 1);
  }

  /** The brightness slider: a multiplier on the preset's exposure. */
  setBrightness(value) {
    this.invalidate();
    this.brightness = value;
    this.renderer.toneMappingExposure = (this.baseExposure ?? 1.05) * value;
  }

  setInterior(on) {
    this.invalidate();
    this.interior = on;
    for (const m of this.interiorMaterials) m.visible = on && m.userData.wanted !== false;
  }

  /**
   * The effects chain: ambient occlusion, then bloom, then tone mapping.
   * Drawn into a multisampled target, because a composer's own targets have
   * no antialiasing and the browser's is lost the moment one is used - hull
   * edges came out stepped with the effects on and smooth with them off.
   * Bloom catches only what is brighter than white once lit: thruster
   * glows, running lights, screens and the hardest highlights.
   */
  setQuality(on) {
    this.invalidate();
    this.quality = on;
    if (on && !this.composer) {
      const size = this.renderer.getDrawingBufferSize(new THREE.Vector2());
      const target = new THREE.WebGLRenderTarget(size.x, size.y, { type: THREE.HalfFloatType, samples: 4 });
      const composer = new EffectComposer(this.renderer, target);
      composer.setPixelRatio(this.renderer.getPixelRatio());
      composer.addPass(new RenderPass(this.scene, this.camera));
      const ao = new GTAOPass(this.scene, this.camera);
      ao.output = GTAOPass.OUTPUT.Default;
      ao.blendIntensity = 0.85;
      this.ao = ao;
      composer.addPass(ao);
      const bloom = new UnrealBloomPass(new THREE.Vector2(size.x, size.y), 0.38, 0.5, 1.7);
      this.bloomPass = bloom;
      composer.addPass(bloom);
      composer.addPass(new OutputPass());
      this.composer = composer;
      this.resize();
    }
  }

  /** Texture size: High is 2048, Ultra the full 4096 the game ships for close looks. */
  async setTextureQuality(max) {
    if (this.textureMax === max) return;
    this.textureMax = max;
    const jobs = [];
    this.root.traverse(o => {
      if (o.isMesh && o.userData.slots && o.userData.dressedFiles)
        jobs.push(this.dress([o], o.userData.dressedFiles, o.userData.dressedPalette));
    });
    await Promise.all(jobs);
  }

  /** A slow turn on its own, for looking rather than handling. */
  setTurntable(on) {
    this.invalidate();
    this.controls.autoRotate = on;
    this.controls.autoRotateSpeed = 0.9;
  }

  /**
   * The loop runs every display frame but draws only when something changed:
   * the camera moved or is still easing to a stop, a texture or part arrived,
   * a setting changed, or the turntable is turning. A still model costs
   * nothing after its last frame - which matters with the game running
   * beside it, since the effects pass is the heaviest thing the view does.
   */
  frame() {
    if (this.disposed) return;
    requestAnimationFrame(this.frame);
    this.stats.frames += 1;
    const moved = this.controls.update();
    if (!moved && !this.dirty && !this.controls.autoRotate) return;
    this.dirty = false;
    this.fill.position.copy(this.camera.position);
    this.fill.target.position.copy(this.controls.target);
    this.fill.target.updateMatrixWorld();
    const started = performance.now();
    if (this.quality && this.composer) this.composer.render();
    else this.renderer.render(this.scene, this.camera);
    this.stats.renders += 1;
    this.stats.renderMs += performance.now() - started;
    this.placeMarkers();
  }

  dispose() {
    this.disposed = true;
    window.removeEventListener('resize', this.resize);
    this.controls.dispose();
    this.scene.traverse(o => {
      if (o.isMesh) {
        o.geometry?.dispose();
        (Array.isArray(o.material) ? o.material : [o.material]).forEach(m => m?.dispose?.());
      }
    });
    this.composer?.dispose?.();
    this.renderer.dispose();
  }

  // --- assembling -----------------------------------------------------------

  async load(cls) {
    this.setStatus(t('Reading the model…'));
    const scene = await getJson(`/api/models/scene/${encodeURIComponent(cls)}`);
    this.sceneInfo = scene;
    this.stamp = scene.stamp;
    this.palettes = scene.palettes || {};
    this.paint = null;
    this.onScene?.(scene);

    const hull = await this.model({ geometry: scene.geometry, material: scene.material, palette: scene.palette, name: scene.name, cls: scene.cls, type: scene.type, port: null }, this.root, true);
    if (!hull) throw new Error(t('The install holds no model the view can read for {name}.', { name: scene.name || cls }));
    this.hull = hull;
    this.frameModel();
    this.setStatus(t('Fitting parts…'));

    if (this.opts.fitted?.length) await this.fit(scene, this.opts.fitted);

    // Parts land as they arrive; the hull is on screen already.
    await this.attach(scene.parts || [], hull, 0);
    this.setStatus('');
    this.frameModel(true);
    this.onParts?.(this.parts);

    if (/^Char_Armor_/i.test(scene.type || '')) {
      await this.suit(cls);
      this.onParts?.(this.parts);
    }
  }

  /**
   * The rest of an armour piece's set - helmet, torso, arms, legs, backpack -
   * drawn around it. Every piece is skinned to the same body at rest, so they
   * stand where they belong without being placed. Shown by default, behind
   * the Full suit box.
   */
  async suit(cls) {
    let got = null;
    try {
      got = await getJson(`/api/models/suit/${encodeURIComponent(cls)}`);
    } catch {
      return;
    }
    if (!got?.pieces?.length || this.disposed) return;
    this.suitParts = [];
    this.onSuit?.(got.pieces);
    this.setStatus(t('Fitting the rest of the suit…'));
    await Promise.all(got.pieces.map(async piece => {
      const scene = await getJson(`/api/models/scene/${encodeURIComponent(piece.cls)}`).catch(() => null);
      if (!scene?.geometry || this.disposed) return;
      Object.assign(this.palettes, scene.palettes || {});
      const group = await this.model({
        geometry: scene.geometry, material: scene.material, palette: scene.palette,
        name: piece.name, cls: piece.cls, type: scene.type, port: piece.part, path: [piece.part],
      }, this.root, false, 0);
      if (group) {
        group.visible = this.showSuit;
        this.suitParts.push(group);
      }
    }));
    this.setStatus('');
    this.frameModel();
  }

  setSuit(on) {
    this.invalidate();
    this.showSuit = on;
    for (const group of this.suitParts || []) group.visible = on;
    this.frameModel();
  }

  /**
   * Puts the pilot's own parts in place of the stock ones before anything is
   * drawn: a Garage build's swapped guns, an Armoury fit's sight. Each fit
   * names the hardpoints from the hull down ("hardpoint_cheek_weapon_left/
   * hardpoint_class_2") and the class to put there; the item's own scene
   * supplies its model and whatever it carries in turn. Shorter paths go
   * first, so a swapped mount is in place before the gun swapped onto it.
   */
  async fit(scene, fits) {
    const ordered = [...fits].filter(f => f?.path).sort((a, b) => a.path.split('/').length - b.path.split('/').length);
    for (const f of ordered) {
      const chain = f.path.toLowerCase().split('/');
      let list = scene.parts || (scene.parts = []);
      let helpers = scene.helpers || {};
      let part = null;
      for (let i = 0; i < chain.length; i++) {
        part = list.find(p => String(p.port).toLowerCase() === chain[i]) || null;
        if (!part && i === chain.length - 1 && f.cls) {
          // A slot the stock loadout leaves empty: an Armoury sight on a gun
          // that ships with iron sights.
          const helper = Object.entries(helpers).find(([k]) => k.toLowerCase() === chain[i])?.[1];
          part = { port: chain[i], helper: f.helper || helper || null, children: [] };
          list.push(part);
        }
        if (!part) break;
        if (i < chain.length - 1) {
          helpers = part.helpers || {};
          list = part.children || (part.children = []);
        }
      }
      if (!part || (part.cls || '').toLowerCase() === String(f.cls || '').toLowerCase()) continue;
      if (!f.cls) {
        Object.assign(part, { cls: null, name: null, geometry: null, children: [], swapped: true });
        continue;
      }
      const item = await getJson(`/api/models/scene/${encodeURIComponent(f.cls)}`).catch(() => null);
      if (!item) continue;
      Object.assign(this.palettes, item.palettes || {});
      Object.assign(part, {
        cls: item.cls, name: item.name, type: item.type, geometry: item.geometry,
        material: item.material, palette: item.palette, helpers: item.helpers || {}, children: item.parts || [], swapped: true,
      });
    }
  }

  /**
   * Puts another attachment in a weapon's slot, or none: the part there now
   * comes off, and the new one is read and hung on the slot's bone, exactly
   * as the stock loadout was.
   */
  async refit(port, cls) {
    const lower = String(port).toLowerCase();
    const going = this.parts.filter(p => p.depth === 0 && String(p.port).toLowerCase() === lower);
    for (const p of going) p.object?.removeFromParent();
    this.parts = this.parts.filter(p => !going.includes(p)
      && !going.some(g => g.object && p.object && isInside(p.object, g.object)));
    if (this.selected && going.includes(this.selected)) this.select(null);
    if (cls) {
      const scene = await getJson(`/api/models/scene/${encodeURIComponent(cls)}`).catch(() => null);
      if (scene?.geometry && !this.disposed) {
        Object.assign(this.palettes, scene.palettes || {});
        const helper = Object.entries(this.sceneInfo.helpers || {}).find(([k]) => k.toLowerCase() === lower)?.[1] || null;
        await this.attach([{
          port, helper, cls: scene.cls, name: scene.name, type: scene.type, geometry: scene.geometry,
          material: scene.material, palette: scene.palette, children: scene.parts || [], swapped: true,
        }], this.hull, 0);
      }
    }
    this.invalidate();
    this.onParts?.(this.parts);
  }

  /**
   * A whole fleet in one scene: each hull with its parts, in its paint, stood
   * side by side on one floor in a row, smallest first, like a hangar line.
   * Ships load one at a time - each sets the view's current scene while its
   * parts find their palettes - and the row is laid out again as each lands.
   */
  async loadFleetShip(entry) {
    if (entry.group || entry.loading) return;
    entry.loading = true;
    this.setStatus(t('Bringing in {ship}…', { ship: entry.name }));
    try {
      const scene = await getJson(`/api/models/scene/${encodeURIComponent(entry.cls)}`);
      if (this.disposed) return;
      this.sceneInfo = scene;
      this.stamp = scene.stamp;
      Object.assign(this.palettes, scene.palettes || {});
      const paint = entry.paint ? (scene.paints || []).find(p => String(p.tag).toLowerCase() === String(entry.paint).toLowerCase()) : null;
      this.paint = paint || null;
      const group = new THREE.Group();
      this.root.add(group);
      const hull = await this.model({ geometry: scene.geometry, material: scene.material, palette: scene.palette, name: scene.name, cls: scene.cls, type: scene.type, port: null }, group, true);
      if (!hull) {
        group.removeFromParent();
        entry.failed = true;
        return;
      }
      this.hull = hull;
      await this.attach(scene.parts || [], hull, 0);
      group.userData.part = { name: entry.name, cls: scene.cls, type: 'Ship', port: null, fleet: true };
      entry.group = group;
      this.parts.push({ ...group.userData.part, object: group, placed: true, depth: -1 });
      this.layoutFleet();
    } catch {
      entry.failed = true;
    } finally {
      entry.loading = false;
      this.setStatus('');
      this.onFleet?.();
    }
  }

  layoutFleet() {
    const shown = (this.fleet || []).filter(e => e.group && e.group.visible);
    const boxes = shown.map(e => {
      e.group.position.set(0, 0, 0);
      this.root.updateMatrixWorld(true);
      const box = new THREE.Box3();
      e.group.traverseVisible(o => {
        if (o.isMesh && o.geometry.boundingBox) box.union(o.geometry.boundingBox.clone().applyMatrix4(o.matrixWorld));
      });
      return { e, box };
    }).filter(x => !x.box.isEmpty())
      .sort((a, b) => (a.box.max.z - a.box.min.z) - (b.box.max.z - b.box.min.z));
    const widest = Math.max(1, ...boxes.map(x => x.box.max.x - x.box.min.x));
    let cursor = 0;
    // The root turns CryEngine's Z-up into three.js's Y-up, so a ship's local
    // x is the row, local z is up and local y runs nose to tail.
    for (const { e, box } of boxes) {
      e.group.position.set(cursor - box.min.x, (box.min.z + box.max.z) / 2, -box.min.y);
      cursor += (box.max.x - box.min.x) + widest * 0.25;
    }
    this.frameModel();
  }

  setFleetShip(entry, on) {
    if (on && !entry.group) {
      this.fleetQueue = (this.fleetQueue || Promise.resolve()).then(() => this.loadFleetShip(entry));
      return;
    }
    if (entry.group) {
      entry.group.visible = on;
      this.layoutFleet();
    }
  }

  async attach(parts, parent, depth, path = []) {
    await Promise.all(parts.map(async raw => {
      const part = { ...raw, path: [...path, raw.port] };
      const node = this.findNode(parent, part.helper) || this.findNode(parent, part.port);
      if (!node) {
        // An empty mount still shows its children at the parent's nodes.
        if (!part.geometry && part.children?.length) await this.attach(part.children, parent, depth + 1, part.path);
        else if (part.geometry) this.parts.push({ ...part, placed: false, depth });
        return;
      }
      if (!part.geometry) {
        if (part.children?.length) await this.attach(part.children, parent, depth + 1, part.path);
        return;
      }
      const model = await this.model(part, node, false, depth);
      if (model && part.children?.length) await this.attach(part.children, model, depth + 1, part.path);
    }));
  }

  findNode(model, port) {
    if (!model || !port) return null;
    const want = port.toLowerCase();
    return model.userData.nodes?.get(want) || null;
  }

  async model(part, parent, isHull, depth = 0) {
    const mesh = await loadMesh(part.geometry, this.stamp);
    if (!mesh || this.disposed) return null;
    const { header } = mesh;

    const group = new THREE.Group();
    group.userData.part = isHull ? null : part;
    group.userData.nodes = new Map();
    const objects = header.nodes.map(n => {
      const o = new THREE.Object3D();
      o.name = n.n;
      o.matrix.fromArray(n.m);
      o.matrixAutoUpdate = false;
      return o;
    });
    header.nodes.forEach((n, i) => {
      (n.p >= 0 && objects[n.p] ? objects[n.p] : group).add(objects[i]);
      group.userData.nodes.set(n.n.toLowerCase(), objects[i]);
    });

    const positions = new THREE.BufferAttribute(mesh.positions, 3);
    const normals = new THREE.InterleavedBufferAttribute(new THREE.InterleavedBuffer(mesh.normals, 4), 3, 0, true);
    const uvs = new THREE.BufferAttribute(mesh.uvs, 2);

    // One geometry per node, one draw group per sub-material run.
    const byNode = new Map();
    header.subsets.forEach(s => {
      if (!byNode.has(s.node)) byNode.set(s.node, []);
      byNode.get(s.node).push(s);
    });

    // The hull wears the paint the view opened in - the one picked for it on
    // Fleet or the Garage - from the first frame, not recoloured after.
    const hullPaint = isHull ? this.paint : null;
    const files = header.materials.map((file, i) => (i === 0 && hullPaint?.material ? hullPaint.material : i === 0 && part.material ? part.material : file));
    const own = isHull ? null : this.palettes[part.palette];
    const palette = own || (this.paint && this.palettes[this.paint.palette]) || this.palettes[this.sceneInfo.palette] || null;

    const meshes = [];
    for (const [nodeIndex, subsets] of byNode) {
      const total = subsets.reduce((a, s) => a + s.count, 0);
      const index = new Uint32Array(total);
      const geometry = new THREE.BufferGeometry();
      geometry.setAttribute('position', positions);
      geometry.setAttribute('normal', normals);
      geometry.setAttribute('uv', uvs);
      let at = 0;
      const slots = [];
      subsets.forEach((s, i) => {
        index.set(mesh.indices.subarray(s.first, s.first + s.count), at);
        geometry.addGroup(at, s.count, i);
        slots.push({ file: s.file, mat: s.mat });
        at += s.count;
      });
      geometry.setIndex(new THREE.BufferAttribute(index, 1));
      // Every node's geometry shares the model's whole vertex buffer, so
      // three.js would measure all of it; measure what this node draws.
      geometry.boundingBox = boundsOf(mesh.positions, index);
      geometry.boundingSphere = geometry.boundingBox.getBoundingSphere(new THREE.Sphere());

      const m = new THREE.Mesh(geometry, slots.map(() => this.fallback));
      m.castShadow = true;
      m.receiveShadow = true;
      m.userData.slots = slots;
      m.userData.files = files;
      m.userData.paletteName = part.palette || null;
      m.userData.isHull = isHull;
      (objects[nodeIndex] || group).add(m);
      meshes.push(m);
    }
    group.userData.meshes = meshes;
    parent.add(group);
    this.invalidate();

    await this.dress(meshes, files, palette);

    if (!isHull) this.parts.push({ ...part, object: group, placed: true, depth });
    return group;
  }

  /** Gives meshes their materials, under a palette; called again when the paint changes. */
  async dress(meshes, files, palette) {
    const subs = await Promise.all(files.map(f => (f ? loadMaterial(f, this.stamp) : null)));
    if (this.disposed) return;
    const made = new Map();
    for (const m of meshes) {
      this.invalidate();
      m.userData.dressedFiles = files;
      m.userData.dressedPalette = palette;
      m.material = m.userData.slots.map(slot => {
        const key = `${slot.file}|${slot.mat}`;
        if (!made.has(key)) {
          const list = subs[slot.file];
          made.set(key, list ? this.materials.build(list[slot.mat], palette) : this.fallback);
        }
        return made.get(key);
      });
    }
  }

  async setPaint(paint) {
    this.paint = paint;
    const name = paint?.palette || this.sceneInfo.palette;
    const palette = this.palettes[name] || null;
    const jobs = [];
    this.root.traverse(o => {
      if (!o.isMesh || !o.userData.slots) return;
      // Weapons and racks carry palettes of their own; the hull, and parts
      // without one, take the livery.
      if (!o.userData.isHull && o.userData.paletteName) return;
      const files = o.userData.isHull && paint?.material
        ? o.userData.files.map((f, i) => (i === 0 ? paint.material : f))
        : o.userData.files;
      jobs.push(this.dress([o], files, palette));
    });
    await Promise.all(jobs);
  }

  frameModel(keepView) {
    this.invalidate();
    const box = new THREE.Box3();
    this.root.updateMatrixWorld(true);
    this.root.traverseVisible(o => {
      if (o.isMesh && o.geometry.boundingBox) box.union(o.geometry.boundingBox.clone().applyMatrix4(o.matrixWorld));
    });
    if (box.isEmpty()) return;
    const size = box.getSize(new THREE.Vector3());
    const centre = box.getCenter(new THREE.Vector3());
    const radius = Math.max(size.x, size.y, size.z) * 0.62;
    this.radius = radius;

    const floorY = box.min.y - radius * 0.02;
    this.floor.position.set(centre.x, floorY, centre.z);
    this.floor.scale.setScalar(radius * 2.4);
    this.grid.position.set(centre.x, floorY + 0.001, centre.z);
    this.grid.scale.setScalar(radius * 1.8);

    this.key.position.set(centre.x + radius * 1.6, centre.y + radius * 2.6, centre.z + radius * 1.2);
    this.key.target.position.copy(centre);
    const cam = this.key.shadow.camera;
    cam.left = cam.bottom = -radius * 1.4;
    cam.right = cam.top = radius * 1.4;
    cam.near = radius * 0.1;
    cam.far = radius * 8;
    cam.updateProjectionMatrix();
    this.rim.position.set(centre.x - radius * 2, centre.y + radius * 0.8, centre.z - radius * 2.2);

    this.camera.near = Math.max(0.01, radius / 200);
    this.camera.far = radius * 40;
    this.camera.updateProjectionMatrix();
    this.controls.minDistance = radius * 0.15;
    this.controls.maxDistance = radius * 8;

    if (!keepView) {
      const distance = radius / Math.sin(THREE.MathUtils.degToRad(this.camera.fov / 2)) * 0.95;
      // Three-quarter view from the front left and a little above: nose is -Z.
      const dir = new THREE.Vector3(-0.72, 0.42, -0.95).normalize();
      this.camera.position.copy(centre).addScaledVector(dir, distance);
      this.controls.target.copy(centre);
      this.controls.update();
    }
    if (this.ao) this.ao.setSize?.(this.renderer.domElement.width, this.renderer.domElement.height);
  }

  view(name) {
    if (!this.radius) return;
    const centre = this.controls.target.clone();
    const d = this.camera.position.distanceTo(centre);
    const dirs = {
      front: [0, 0.08, -1], back: [0, 0.12, 1], left: [-1, 0.1, 0], right: [1, 0.1, 0],
      top: [0.0001, 1, -0.0001], three: [-0.72, 0.42, -0.95],
    };
    const dir = new THREE.Vector3(...(dirs[name] || dirs.three)).normalize();
    this.camera.position.copy(centre).addScaledVector(dir, d);
    this.controls.update();
  }

  // --- picking --------------------------------------------------------------

  pick(e) {
    const rect = this.renderer.domElement.getBoundingClientRect();
    const pointer = new THREE.Vector2(((e.clientX - rect.left) / rect.width) * 2 - 1, -((e.clientY - rect.top) / rect.height) * 2 + 1);
    this.raycaster.setFromCamera(pointer, this.camera);
    const hits = this.raycaster.intersectObject(this.root, true).filter(h => h.object.visible && h.object.isMesh);
    for (const hit of hits) {
      let o = hit.object;
      while (o && o !== this.root) {
        if (o.userData.part) {
          this.select(this.parts.find(p => p.object === o) || null);
          return;
        }
        o = o.parent;
      }
    }
    this.select(null);
  }

  select(part) {
    this.invalidate();
    this.selected = part;
    if (this.box) {
      this.scene.remove(this.box);
      this.box.geometry.dispose();
      this.box = null;
    }
    if (part?.object) {
      this.box = new THREE.BoxHelper(part.object, 0x5ce1ff);
      this.box.material.depthTest = false;
      this.box.material.transparent = true;
      this.box.renderOrder = 10;
      this.scene.add(this.box);
    }
    this.onSelect?.(part);
  }

  focus(part) {
    if (!part?.object) return;
    const box = new THREE.Box3().setFromObject(part.object);
    const centre = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3()).length();
    const dir = this.camera.position.clone().sub(this.controls.target).normalize();
    this.controls.target.copy(centre);
    this.camera.position.copy(centre).addScaledVector(dir, Math.max(size * 2.2, this.radius * 0.35));
    this.controls.update();
  }

  placeMarkers() {
    const layer = this.host.querySelector('.v3d-markers');
    if (!layer || layer.hidden) return;
    const rect = this.renderer.domElement.getBoundingClientRect();
    const v = new THREE.Vector3();
    for (const el of layer.children) {
      const part = this.parts[+el.dataset.part];
      if (!part?.object) { el.hidden = true; continue; }
      part.object.getWorldPosition(v);
      v.project(this.camera);
      const behind = v.z > 1;
      el.hidden = behind;
      el.style.transform = `translate(${((v.x + 1) / 2) * rect.width}px, ${((1 - v.y) / 2) * rect.height}px)`;
      el.classList.toggle('on', part === this.selected);
    }
  }
}

// --- the overlay ------------------------------------------------------------

let current = null;

function shell() {
  const host = document.createElement('div');
  host.className = 'v3d';
  host.setAttribute('role', 'dialog');
  host.setAttribute('aria-modal', 'true');
  host.innerHTML = `
    <div class="v3d-stage">
      <canvas class="v3d-canvas" aria-label="${escHtml(t('3D model'))}"></canvas>
      <div class="v3d-markers"></div>
      <div class="v3d-status" data-v3d-status></div>
    </div>
    <header class="v3d-bar">
      <div class="v3d-title"><span class="v3d-kicker">${escHtml(t('3D view'))}</span><strong data-v3d-name></strong><span class="v3d-sub" data-v3d-sub></span></div>
      <div class="v3d-tools">
        <div class="v3d-views" role="group" aria-label="${escHtml(t('Camera'))}">
          <button type="button" data-v3d-view="three">${escHtml(t('¾'))}</button>
          <button type="button" data-v3d-view="front">${escHtml(t('Front'))}</button>
          <button type="button" data-v3d-view="left">${escHtml(t('Profile'))}</button>
          <button type="button" data-v3d-view="top">${escHtml(t('Top'))}</button>
          <button type="button" data-v3d-view="back">${escHtml(t('Rear'))}</button>
        </div>
        <label class="v3d-toggle"><input type="checkbox" data-v3d-markers checked> ${escHtml(t('Slots'))}</label>
        <label class="v3d-toggle"><input type="checkbox" data-v3d-interior> ${escHtml(t('Interior'))}</label>
        <label class="v3d-toggle"><input type="checkbox" data-v3d-turntable> ${escHtml(t('Turntable'))}</label>
        <label class="v3d-toggle" data-v3d-suit-box hidden><input type="checkbox" data-v3d-suit checked> ${escHtml(t('Full suit'))}</label>
        <select class="v3d-select" data-v3d-lighting aria-label="${escHtml(t('Lighting'))}" title="${escHtml(t('Lighting'))}">
          <option value="studio">${escHtml(t('Studio light'))}</option>
          <option value="hangar">${escHtml(t('Hangar light'))}</option>
          <option value="space">${escHtml(t('Deep space'))}</option>
        </select>
        <label class="v3d-toggle" title="${escHtml(t('Brightness'))}">☀ <input type="range" data-v3d-brightness min="0.5" max="2.2" step="0.05" value="1" aria-label="${escHtml(t('Brightness'))}"></label>
        <label class="v3d-toggle" title="${escHtml(t('Ambient occlusion, glow and smoothed edges'))}"><input type="checkbox" data-v3d-quality checked> ${escHtml(t('Effects'))}</label>
        <select class="v3d-select" data-v3d-textures aria-label="${escHtml(t('Texture quality'))}" title="${escHtml(t('Texture quality'))}">
          <option value="2048">${escHtml(t('High textures'))}</option>
          <option value="4096">${escHtml(t('Ultra textures'))}</option>
        </select>
        <button type="button" class="v3d-shot" data-v3d-shot>${escHtml(t('Save picture'))}</button>
        <button type="button" class="v3d-close" data-v3d-close aria-label="${escHtml(t('Close'))}">✕</button>
      </div>
    </header>
    <aside class="v3d-side">
      <section class="v3d-paints" data-v3d-paints hidden>
        <h3>${escHtml(t('Paint'))}</h3>
        <select data-v3d-paint aria-label="${escHtml(t('Paint'))}"></select>
        <p class="v3d-note">${escHtml(t('The game tints its hulls from a palette of three colours; this draws the palette, not the wear and grime the engine adds.'))}</p>
      </section>
      <section class="v3d-detail" data-v3d-detail hidden></section>
      <section class="v3d-attach" data-v3d-attach hidden>
        <h3>${escHtml(t('Attachments'))}</h3>
        <div data-v3d-attach-list></div>
        <p class="v3d-note">${escHtml(t('What fits each slot, by the game files\' sizes and tags. The same choice as the Armoury\'s.'))}</p>
      </section>
      <section class="v3d-parts">
        <h3>${escHtml(t('Fitted parts'))}</h3>
        <p class="v3d-note" data-v3d-parts-note>${escHtml(t('The stock loadout the game fits. Click a part, here or on the model.'))}</p>
        <div data-v3d-parts></div>
      </section>
    </aside>
    <div class="v3d-progress" data-v3d-progress hidden></div>`;
  return host;
}

function partLabel(p) {
  return p.name && p.name !== p.cls ? p.name : (p.cls || p.port);
}

function portLabel(port) {
  return String(port || '').replace(/^hardpoint_/i, '').replace(/_/g, ' ');
}

/** Where a part sits, mount by mount: "cheek weapon left › class 2". */
function slotLabel(part) {
  return (part.path || [part.port]).map(portLabel).join(' › ');
}

/**
 * Opens the view over the page. Escape or the close button take it down.
 * @param {{cls: string, title?: string}} opts
 */
export async function openModel(opts) {
  closeModel();
  const host = shell();
  document.body.appendChild(host);
  document.body.classList.add('v3d-open');

  const q = s => host.querySelector(s);
  q('[data-v3d-name]').textContent = opts.title || opts.cls;
  if (opts.fitted?.some(f => f?.cls)) {
    q('[data-v3d-parts-note]').textContent = t('The stock loadout with your changes in place, marked. Click a part, here or on the model.');
  }

  if (!window.WebGL2RenderingContext) {
    q('[data-v3d-status]').textContent = t('This browser cannot draw 3D (no WebGL 2).');
    q('[data-v3d-close]').addEventListener('click', closeModel);
    current = { host, view: null };
    return;
  }

  let view;
  try {
    view = new ModelView(host, opts);
  } catch (err) {
    q('[data-v3d-status]').textContent = t('This browser cannot draw 3D: {why}', { why: err.message });
    q('[data-v3d-close]').addEventListener('click', closeModel);
    current = { host, view: null };
    return;
  }
  current = { host, view };
  window.QW3D.view = view;
  view.setQuality(q('[data-v3d-quality]').checked);

  const onKey = e => { if (e.key === 'Escape') closeModel(); };
  document.addEventListener('keydown', onKey);
  current.onKey = onKey;

  q('[data-v3d-close]').addEventListener('click', closeModel);
  host.querySelectorAll('[data-v3d-view]').forEach(b => b.addEventListener('click', () => view.view(b.dataset.v3dView)));
  q('[data-v3d-quality]').addEventListener('change', e => view.setQuality(e.target.checked));
  q('[data-v3d-interior]').addEventListener('change', e => view.setInterior(e.target.checked));
  q('[data-v3d-turntable]').addEventListener('change', e => view.setTurntable(e.target.checked));
  q('[data-v3d-suit]').addEventListener('change', e => view.setSuit(e.target.checked));
  view.onSuit = () => { q('[data-v3d-suit-box]').hidden = false; };
  q('[data-v3d-lighting]').addEventListener('change', e => view.setLighting(e.target.value));
  q('[data-v3d-textures]').addEventListener('change', e => view.setTextureQuality(Number(e.target.value)));
  q('[data-v3d-brightness]').addEventListener('input', e => view.setBrightness(Number(e.target.value)));
  view.setLighting(opts.lighting || 'studio');
  q('[data-v3d-markers]').addEventListener('change', e => { q('.v3d-markers').hidden = !e.target.checked; view.invalidate(); });
  q('[data-v3d-shot]').addEventListener('click', () => {
    const a = document.createElement('a');
    a.href = view.renderer.domElement.toDataURL('image/png');
    a.download = `${(view.sceneInfo?.name || opts.cls).replace(/[^\w.-]+/g, '_')}.png`;
    a.click();
  });

  view.onScene = scene => {
    q('[data-v3d-name]').textContent = opts.title || scene.name || scene.cls;
    q('[data-v3d-sub]').textContent = scene.vehicle ? scene.cls : (scene.type || '');
    const paints = scene.paints || [];
    if (paints.length) {
      const select = q('[data-v3d-paint]');
      select.innerHTML = `<option value="">${escHtml(t('Factory finish'))}</option>` + paints
        .map((p, i) => `<option value="${i}">${escHtml(p.name)}</option>`).join('');
      select.addEventListener('change', () => view.setPaint(select.value === '' ? null : paints[+select.value]));
      q('[data-v3d-paints]').hidden = false;
      // The paint item the pilot chose for this hull elsewhere in the app is
      // the paint's tag here; open in it.
      const wanted = opts.paint ? paints.findIndex(p => String(p.tag).toLowerCase() === String(opts.paint).toLowerCase()) : -1;
      if (wanted >= 0) {
        select.value = String(wanted);
        view.paint = paints[wanted];
      }
    }
  };

  view.onParts = parts => renderParts(host, view, parts);
  view.onSelect = part => renderDetail(host, view, part);

  if (opts.fleet?.length) {
    openFleet(host, view, opts);
    return;
  }

  try {
    await view.load(opts.cls);
  } catch (err) {
    view.setStatus(err.message || String(err));
    return;
  }

  // A personal weapon's slots: what fits, from the Armoury's own reading,
  // shared with it so a pick here is the Armoury's pick too.
  if (/^WeaponPersonal\//i.test(view.sceneInfo?.type || '') && typeof window.qwModelSlots === 'function') {
    const slots = await window.qwModelSlots(view.sceneInfo.cls).catch(() => null);
    if (slots?.slots?.length && !view.disposed) renderAttachments(host, view, slots);
  }
}

/** How many ships a fleet scene starts with; the rest wait in the list, unticked. */
const FLEET_START = 8;

function openFleet(host, view, opts) {
  const q = sel => host.querySelector(sel);
  view.fleet = opts.fleet.map(e => ({ ...e }));
  view.palettes = {};
  view.textureMax = 1024;
  view.sceneInfo = { palette: null, helpers: {} };
  q('[data-v3d-sub]').textContent = t('{count} ships', { count: view.fleet.length });
  q('[data-v3d-paints]').hidden = true;
  q('.v3d-parts h3').textContent = t('Your fleet');
  q('[data-v3d-parts-note]').textContent = t('Tick the ships to stand in the row. Click one, here or in the scene, to open it alone.');
  q('.v3d-markers').hidden = true;

  const list = q('[data-v3d-parts]');
  const draw = () => {
    list.innerHTML = '';
    const ul = document.createElement('ul');
    ul.className = 'v3d-fleet';
    view.fleet.forEach(entry => {
      const li = document.createElement('li');
      const box = document.createElement('input');
      box.type = 'checkbox';
      box.checked = !!entry.on;
      box.disabled = !!entry.failed;
      box.addEventListener('change', () => {
        entry.on = box.checked;
        view.setFleetShip(entry, entry.on);
      });
      const name = document.createElement('button');
      name.type = 'button';
      name.textContent = entry.name;
      name.title = t('Open {ship} alone', { ship: entry.name });
      name.addEventListener('click', () => openModel({ cls: entry.cls, title: entry.name, paint: entry.paint }));
      const state = document.createElement('small');
      state.textContent = entry.failed ? t('no model the view can read') : entry.loading ? t('loading…') : '';
      li.append(box, name, state);
      ul.append(li);
    });
    list.append(ul);
  };
  view.onFleet = draw;
  view.onParts = () => {};
  view.onSelect = part => {
    renderDetail(host, view, part?.fleet ? null : part);
    if (part?.fleet) {
      const detail = q('[data-v3d-detail]');
      detail.hidden = false;
      detail.innerHTML = `<h3>${escHtml(part.name)}</h3><button type="button" data-v3d-alone>${escHtml(t('Open alone'))}</button>`;
      const entry = view.fleet.find(e => e.group === part.object);
      detail.querySelector('[data-v3d-alone]').addEventListener('click', () =>
        openModel({ cls: part.cls, title: part.name, paint: entry?.paint }));
    }
  };

  view.fleet.forEach((entry, i) => { entry.on = i < FLEET_START; });
  draw();
  for (const entry of view.fleet) if (entry.on) view.setFleetShip(entry, true);
}

function renderAttachments(host, view, slots) {
  const box = host.querySelector('[data-v3d-attach]');
  const list = host.querySelector('[data-v3d-attach-list]');
  list.innerHTML = '';
  for (const slot of slots.slots) {
    const row = document.createElement('label');
    row.className = 'v3d-attach-slot';
    const word = document.createElement('span');
    word.textContent = slot.label;
    const select = document.createElement('select');
    select.disabled = slot.fixed;
    select.append(new Option(t('Nothing'), ''));
    for (const o of slot.options) select.append(new Option(o.cls === slot.default ? `${o.name} · ${t('ships with it')}` : o.name, o.cls));
    select.value = slot.chosen || '';
    select.addEventListener('change', () => {
      slots.choose(slot.slot, select.value);
      view.refit(slot.port, select.value || null);
    });
    row.append(word, select);
    list.append(row);

    // The Armoury's choice may already differ from the stock fit drawn.
    const drawn = view.parts.find(p => p.depth === 0 && String(p.port).toLowerCase() === String(slot.port).toLowerCase());
    if ((drawn?.cls || '') !== (slot.chosen || '') && !slot.fixed) view.refit(slot.port, slot.chosen || null);
  }
  box.hidden = false;
}

function renderParts(host, view, parts) {
  const list = host.querySelector('[data-v3d-parts]');
  const placed = parts.map((p, i) => ({ p, i })).filter(({ p }) => p.placed);
  const groups = new Map();
  placed.forEach(({ p, i }) => {
    const g = groupOf(p.type);
    if (!groups.has(g)) groups.set(g, []);
    groups.get(g).push({ p, i });
  });
  const order = [...new Set(PART_GROUPS.map(([, g]) => g)), 'Other parts'];
  list.innerHTML = order.filter(g => groups.has(g)).map(g => {
    const rows = groups.get(g);
    return `<div class="v3d-group"><h4>${escHtml(t(g))} <span>${rows.length}</span></h4><ul>${rows.map(({ p, i }) =>
      `<li><button type="button" data-v3d-part="${i}" style="--depth:${p.depth}"><span>${escHtml(partLabel(p))}${p.swapped ? ` <em class="v3d-mine">${escHtml(t('your fit'))}</em>` : ''}</span><small>${escHtml(slotLabel(p))}</small></button></li>`).join('')}</ul></div>`;
  }).join('') || `<p class="v3d-note">${escHtml(t('No fitted parts the view can draw.'))}</p>`;

  list.querySelectorAll('[data-v3d-part]').forEach(b => b.addEventListener('click', () => {
    const part = parts[+b.dataset.v3dPart];
    view.select(part);
    view.focus(part);
  }));

  // One dot per weapon-like part, not per thruster: forty dots on a hull
  // read as noise.
  const markers = host.querySelector('.v3d-markers');
  markers.innerHTML = parts.map((p, i) => ({ p, i }))
    .filter(({ p }) => p.placed && /^(WeaponGun|WeaponMining|TractorBeam|Turret|TurretBase|MissileLauncher|BombLauncher|WeaponDefensive)\//.test(p.type || '') && p.depth <= 1)
    .map(({ p, i }) => `<button type="button" class="v3d-marker" data-part="${i}" title="${escHtml(partLabel(p))}"><span></span></button>`).join('');
  markers.querySelectorAll('.v3d-marker').forEach(b => b.addEventListener('click', () => view.select(parts[+b.dataset.part])));
}

function renderDetail(host, view, part) {
  const box = host.querySelector('[data-v3d-detail]');
  host.querySelectorAll('[data-v3d-part]').forEach(b => b.classList.toggle('on', parts(view)[+b.dataset.v3dPart] === part));
  if (!part) { box.hidden = true; box.innerHTML = ''; return; }
  const inside = view.parts.filter(p => p !== part && p.object && part.object && isInside(p.object, part.object));
  box.hidden = false;
  box.innerHTML = `
    <h3>${escHtml(partLabel(part))}</h3>
    <dl>
      <dt>${escHtml(t('Slot'))}</dt><dd>${escHtml(slotLabel(part))}</dd>
      <dt>${escHtml(t('Kind'))}</dt><dd>${escHtml(t(groupOf(part.type)))}</dd>
      ${part.cls ? `<dt>${escHtml(t('Class'))}</dt><dd><code>${escHtml(part.cls)}</code></dd>` : ''}
      ${inside.length ? `<dt>${escHtml(t('Carrying'))}</dt><dd>${inside.map(p => escHtml(partLabel(p))).join('<br>')}</dd>` : ''}
    </dl>
    <button type="button" data-v3d-focus>${escHtml(t('Look closer'))}</button>`;
  box.querySelector('[data-v3d-focus]').addEventListener('click', () => view.focus(part));
}

function parts(view) { return view.parts; }

function isInside(child, parent) {
  for (let o = child.parent; o; o = o.parent) if (o === parent) return true;
  return false;
}

export function closeModel() {
  if (!current) return;
  current.view?.dispose();
  if (current.onKey) document.removeEventListener('keydown', current.onKey);
  current.host.remove();
  document.body.classList.remove('v3d-open');
  current = null;
}

window.QW3D = { openModel, closeModel, textureLog };
window.dispatchEvent(new Event('qw3d-ready'));
