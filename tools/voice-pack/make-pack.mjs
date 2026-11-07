// Records the combat callout voice pack: every phrase the combat watch says and
// every hull name it can put in one, each once, in one Kokoro voice. The app
// joins them when a callout fires, so it speaks at once instead of waiting
// ~3 s a sentence for the model on the pilot's CPU (measured on 2026-11-06).
//
//   npm install                                    (once; fetches ~90 MB of model; needs ffmpeg on PATH)
//   dotnet run --project ..\..\src\Quantumwake.Cli -c Release -- --voice-names > names.txt
//   node make-pack.mjs names.txt ..\..\src\Quantumwake.Ocr\Voices af_bella
//
// Kokoro-82M and kokoro-js are Apache-2.0; the recordings are ours to ship.
// Clips are trimmed of the silence at either end, so joining them leaves no
// dead air between words, then made AAC at 40 kbps with ffmpeg: the WAVs came
// to 18 MB, too much to embed in a single-file app, and the AAC to 2.7 MB.
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { KokoroTTS } from 'kokoro-js';

const [namesFile, outRoot, voice = 'af_bella'] = process.argv.slice(2);
if (!namesFile || !outRoot) {
  console.error('usage: node make-pack.mjs <names.txt> <out folder> [voice]');
  process.exit(1);
}

// The pieces CombatWatch and MiningWatch build their sentences from.
const phrases = [
  'Hostile target identified', 'Target identified', 'Target disabled', 'Target critical',
  'Hostile contact, likely', 'Unknown contact, likely', 'Hostile contact', 'Unknown contact',
  'Hostile', 'likely', 'or', 'wanted',
  'In the window', 'Out of the window', 'Overcharge, back off', 'Rock cracked',
  'Missile inbound', 'Entering hostile territory', 'Escort hull', 'Objective health', 'below 50 percent', 'below 25 percent',
  'Quantum Wake system online', 'New objective', 'Objective complete', 'Objective failed', 'Contract complete', 'Contract failed',
  // How many rocks a ping is: PingReadout.Sentence, up to its twenty.
  'One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven', 'Eight', 'Nine', 'Ten',
  'Eleven', 'Twelve', 'Thirteen', 'Fourteen', 'Fifteen', 'Sixteen', 'Seventeen', 'Eighteen', 'Nineteen', 'Twenty',
  // The voice-command copilot's replies (1.2.2): VoiceCommands.Spoken(), which
  // VoicePackTests holds the embedded pack to, so a reply added there and
  // not here fails the build rather than falling back to the Windows voice.
  'Copilot on', 'Copilot off', 'Cancelled', "Can't press that", 'Game not in front',
  'Guns', 'Mining mode', 'Salvage mode', 'Scan mode', 'Quantum mode', 'Combat mode', 'Nav mode',
  'Ping', 'Scanning', 'Laser', 'Switching laser', 'Beam switched', 'Module one', 'Module two', 'Module three',
  'More power', 'Less power', 'Cargo jettisoned', 'Doors open', 'Doors closed', 'Shields to max',
  'Requesting clearance', 'Self destruct', 'Self destruct stopped', 'Confirm jettison?', 'Confirm self destruct?',
];

// Spelled for the ear, not the eye: Mk II is "Mark Two", the C.O. hulls are
// "C O", and the letter after a dash is a letter of its own.
const spoken = (name) => name
  .replace(/\bMk II\b/g, 'Mark Two').replace(/\bMk I\b/g, 'Mark One').replace(/\bMk\b/g, 'Mark')
  .replace(/\bC\.O\./g, 'C O').replace(/(\w)-(\w)/g, '$1 $2');

const slug = (text) => text.toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_|_$/g, '');

const names = fs.readFileSync(namesFile, 'utf8').split(/\r?\n/).map(s => s.trim()).filter(Boolean);
const outDir = path.join(outRoot, voice);
fs.mkdirSync(outDir, { recursive: true });

const tts = await KokoroTTS.from_pretrained('onnx-community/Kokoro-82M-v1.0-ONNX', { dtype: 'q8', device: 'cpu' });

/** Float samples to a 16-bit mono WAV, trimmed to the speech with 30 ms either side. */
function wav(samples, rate) {
  const quiet = 0.01, margin = Math.round(rate * 0.03);
  let start = 0, end = samples.length;
  while (start < end && Math.abs(samples[start]) < quiet) start++;
  while (end > start && Math.abs(samples[end - 1]) < quiet) end--;
  start = Math.max(0, start - margin); end = Math.min(samples.length, end + margin);
  const count = end - start, buffer = Buffer.alloc(44 + count * 2);
  buffer.write('RIFF', 0); buffer.writeUInt32LE(36 + count * 2, 4); buffer.write('WAVE', 8);
  buffer.write('fmt ', 12); buffer.writeUInt32LE(16, 16); buffer.writeUInt16LE(1, 20); buffer.writeUInt16LE(1, 22);
  buffer.writeUInt32LE(rate, 24); buffer.writeUInt32LE(rate * 2, 28); buffer.writeUInt16LE(2, 32); buffer.writeUInt16LE(16, 34);
  buffer.write('data', 36); buffer.writeUInt32LE(count * 2, 40);
  for (let i = 0; i < count; i++) buffer.writeInt16LE(Math.max(-32768, Math.min(32767, Math.round(samples[start + i] * 32767))), 44 + i * 2);
  return buffer;
}

// An existing pack is added to, not remade: clips already recorded are kept,
// so a new phrase or hull does not rewrite 2.7 MB of unchanged audio.
const manifestPath = path.join(outDir, 'manifest.json');
const manifest = fs.existsSync(manifestPath) ? JSON.parse(fs.readFileSync(manifestPath, 'utf8'))
  : { voice, model: 'Kokoro-82M-v1.0 (Apache-2.0)', phrases: {}, names: {} };
async function record(text, said, kind) {
  const stem = `${kind === 'phrases' ? 'p' : 'n'}_${slug(text)}`;
  if (manifest[kind][text] && fs.existsSync(path.join(outDir, manifest[kind][text]))) return;
  const audio = await tts.generate(said, { voice });
  const raw = path.join(outDir, stem + '.wav');
  fs.writeFileSync(raw, wav(audio.audio, audio.sampling_rate));
  execFileSync('ffmpeg', ['-v', 'error', '-y', '-i', raw, '-c:a', 'aac', '-b:a', '40k', '-ac', '1', path.join(outDir, stem + '.m4a')]);
  fs.unlinkSync(raw);
  manifest[kind][text] = stem + '.m4a';
}

for (const phrase of phrases) await record(phrase, phrase, 'phrases');
let done = 0;
for (const name of names) {
  await record(name, spoken(name), 'names');
  if (++done % 25 === 0) console.log(`${done} of ${names.length} names`);
}
fs.writeFileSync(path.join(outDir, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
console.log(`${phrases.length} phrases and ${names.length} names in ${outDir}`);
