# three.js r170, vendored

From the npm package `three@0.170.0` (MIT, see LICENSE): `build/three.module.min.js`
and, under `addons/`, the files from `examples/jsm/` the 3D view uses (orbit
controls, the room environment, the ambient-occlusion passes). The addons'
`import ... from 'three'` lines point at `../../three.module.min.js` instead, so
no import map is needed.

One local change, in `three.module.min.js`: `WebGLUtils.convert` maps
`RED_RGTC1_Format` by comparing against the BPTC constant in r170, so it returns
null and an unsigned BC4 texture - every gloss map the game ships - is refused
with "Attempt to load unsupported compressed texture format". The comparison
now uses the RGTC1 constant (minified `we`, 36283):

    if(n===we)return r.COMPRESSED_RED_RGTC1_EXT

Re-apply when upgrading, or drop it once upstream has the fix.
