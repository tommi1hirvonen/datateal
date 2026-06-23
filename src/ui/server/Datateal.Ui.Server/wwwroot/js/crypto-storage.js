// Encrypted localStorage for sensitive client-side data (AI provider API keys).
// Uses AES-256-GCM via the Web Crypto API (SubtleCrypto) — no server round-trip.
// Both the encryption key (as JWK) and ciphertext live in localStorage, scoped
// to this origin. Data is cleared when the user clears site data
//
// Security note: XSS in the same origin can read plaintext at runtime.
// Mitigation: strict CSP headers + HtmlSanitizer on all rendered user content.

(function () {
  const ALGO = { name: 'AES-GCM', length: 256 };
  const KEY_PREFIX = 'dh-keymat:';
  const DATA_PREFIX = 'dh-enc:';

  function toBase64(buffer) {
    return btoa(String.fromCharCode(...new Uint8Array(buffer)));
  }

  function fromBase64(b64) {
    return Uint8Array.from(atob(b64), (c) => c.charCodeAt(0));
  }

  async function getOrCreateKey(keyId) {
    const stored = localStorage.getItem(KEY_PREFIX + keyId);
    if (stored) {
      const jwk = JSON.parse(stored);
      return await crypto.subtle.importKey('jwk', jwk, ALGO, false, ['encrypt', 'decrypt']);
    }
    const key = await crypto.subtle.generateKey(ALGO, true, ['encrypt', 'decrypt']);
    const jwk = await crypto.subtle.exportKey('jwk', key);
    localStorage.setItem(KEY_PREFIX + keyId, JSON.stringify(jwk));
    // Re-import as non-extractable for runtime use
    return await crypto.subtle.importKey('jwk', jwk, ALGO, false, ['encrypt', 'decrypt']);
  }

  window.dhEncryptedStorage = {
    async setItem(keyId, plaintext) {
      const key = await getOrCreateKey(keyId);
      const iv = crypto.getRandomValues(new Uint8Array(12)); // fresh IV every write
      const encoded = new TextEncoder().encode(plaintext);
      const ciphertext = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, key, encoded);
      const payload = JSON.stringify({ iv: toBase64(iv), ct: toBase64(ciphertext) });
      localStorage.setItem(DATA_PREFIX + keyId, payload);
    },

    async getItem(keyId) {
      const raw = localStorage.getItem(DATA_PREFIX + keyId);
      if (!raw) return null;
      try {
        const { iv, ct } = JSON.parse(raw);
        const key = await getOrCreateKey(keyId);
        const decrypted = await crypto.subtle.decrypt(
          { name: 'AES-GCM', iv: fromBase64(iv) },
          key,
          fromBase64(ct)
        );
        return new TextDecoder().decode(decrypted);
      } catch {
        return null; // Tampered or key mismatch
      }
    },

    removeItem(keyId) {
      localStorage.removeItem(DATA_PREFIX + keyId);
      localStorage.removeItem(KEY_PREFIX + keyId);
    },

    async hasItem(keyId) {
      return localStorage.getItem(DATA_PREFIX + keyId) !== null;
    },
  };
})();
