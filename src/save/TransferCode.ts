/** Код переноса: Base64url(JSON) + CRC32. Без внешних библиотек. */
function crc32(bytes: Uint8Array): number {
  let c = 0xffffffff;
  for (let i = 0; i < bytes.length; i++) {
    c ^= bytes[i] ?? 0;
    for (let k = 0; k < 8; k++) c = (c >>> 1) ^ (0xedb88320 & -(c & 1));
  }
  return (c ^ 0xffffffff) >>> 0;
}

function toBase64Url(bytes: Uint8Array): string {
  let s = '';
  for (let i = 0; i < bytes.length; i++) s += String.fromCharCode(bytes[i] ?? 0);
  return btoa(s).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function fromBase64Url(text: string): Uint8Array {
  const pad = text.length % 4 === 0 ? '' : '='.repeat(4 - (text.length % 4));
  const s = atob(text.replace(/-/g, '+').replace(/_/g, '/') + pad);
  const out = new Uint8Array(s.length);
  for (let i = 0; i < s.length; i++) out[i] = s.charCodeAt(i);
  return out;
}

export function encodeTransfer(data: unknown): string {
  const json = new TextEncoder().encode(JSON.stringify(data));
  const sum = crc32(json);
  const payload = new Uint8Array(json.length + 4);
  payload.set(json, 0);
  payload[json.length] = (sum >>> 24) & 0xff;
  payload[json.length + 1] = (sum >>> 16) & 0xff;
  payload[json.length + 2] = (sum >>> 8) & 0xff;
  payload[json.length + 3] = sum & 0xff;
  return `ORB1-${toBase64Url(payload)}`;
}

export function decodeTransfer<T>(code: string): T | null {
  if (!code.startsWith('ORB1-')) return null;
  try {
    const payload = fromBase64Url(code.slice(5));
    if (payload.length < 5) return null;
    const json = payload.subarray(0, payload.length - 4);
    const expected =
      ((payload[payload.length - 4] ?? 0) << 24) |
      ((payload[payload.length - 3] ?? 0) << 16) |
      ((payload[payload.length - 2] ?? 0) << 8) |
      (payload[payload.length - 1] ?? 0);
    if ((expected >>> 0) !== crc32(json)) return null;
    return JSON.parse(new TextDecoder().decode(json)) as T;
  } catch {
    return null;
  }
}
