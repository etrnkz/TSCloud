use crate::{Result, TSCloudError};
use argon2::{Argon2, PasswordHash, PasswordHasher, PasswordVerifier};
use argon2::password_hash::{rand_core::OsRng, SaltString};
use chacha20poly1305::{XChaCha20Poly1305, Key, XNonce, aead::{Aead, KeyInit, OsRng as AeadOsRng}};
use blake3::Hasher;
use rand::RngCore;

pub const KEY_SIZE: usize = 32;
pub const NONCE_SIZE: usize = 24;
pub const SALT_SIZE: usize = 32;

#[derive(Debug, Clone)]
pub struct MasterKey {
    key: [u8; KEY_SIZE],
}

impl MasterKey {
    pub fn derive_from_password(password: &str, salt: &[u8]) -> Result<Self> {
        if salt.len() != SALT_SIZE {
            return Err(TSCloudError::Crypto("Invalid salt size".to_string()));
        }

        let argon2 = Argon2::default();
        let salt_string = SaltString::encode_b64(salt)
            .map_err(|e| TSCloudError::Crypto(format!("Salt encoding error: {}", e)))?;
        
        let password_hash = argon2
            .hash_password(password.as_bytes(), &salt_string)
            .map_err(|e| TSCloudError::Crypto(format!("Key derivation error: {}", e)))?;

        let mut key = [0u8; KEY_SIZE];
        key.copy_from_slice(&password_hash.hash.unwrap().as_bytes()[..KEY_SIZE]);

        Ok(Self { key })
    }

    pub fn from_bytes(key_bytes: [u8; KEY_SIZE]) -> Self {
        Self { key: key_bytes }
    }

    pub fn generate_salt() -> [u8; SALT_SIZE] {
        let mut salt = [0u8; SALT_SIZE];
        OsRng.fill_bytes(&mut salt);
        salt
    }

    pub fn as_bytes(&self) -> &[u8; KEY_SIZE] {
        &self.key
    }

    pub fn encrypt_data(&self, data: &[u8]) -> Result<(Vec<u8>, [u8; NONCE_SIZE])> {
        let encryptor = ChunkEncryptor::new(self);
        encryptor.encrypt_chunk(data)
    }

    pub fn decrypt_data(&self, ciphertext: &[u8], nonce: &[u8; NONCE_SIZE]) -> Result<Vec<u8>> {
        let encryptor = ChunkEncryptor::new(self);
        encryptor.decrypt_chunk(ciphertext, nonce)
    }
}

pub struct ChunkEncryptor {
    cipher: XChaCha20Poly1305,
}

impl ChunkEncryptor {
    pub fn new(master_key: &MasterKey) -> Self {
        let key = Key::from_slice(master_key.as_bytes());
        let cipher = XChaCha20Poly1305::new(key);
        Self { cipher }
    }

    pub fn encrypt_chunk(&self, data: &[u8]) -> Result<(Vec<u8>, [u8; NONCE_SIZE])> {
        let mut nonce_bytes = [0u8; NONCE_SIZE];
        AeadOsRng.fill_bytes(&mut nonce_bytes);
        let nonce = XNonce::from_slice(&nonce_bytes);

        let ciphertext = self.cipher
            .encrypt(nonce, data)
            .map_err(|e| TSCloudError::Crypto(format!("Encryption error: {}", e)))?;

        Ok((ciphertext, nonce_bytes))
    }

    pub fn decrypt_chunk(&self, ciphertext: &[u8], nonce: &[u8; NONCE_SIZE]) -> Result<Vec<u8>> {
        let nonce = XNonce::from_slice(nonce);
        
        let plaintext = self.cipher
            .decrypt(nonce, ciphertext)
            .map_err(|e| TSCloudError::Crypto(format!("Decryption error: {}", e)))?;

        Ok(plaintext)
    }
}

pub fn hash_data(data: &[u8]) -> [u8; 32] {
    let mut hasher = Hasher::new();
    hasher.update(data);
    *hasher.finalize().as_bytes()
}

pub fn verify_hash(data: &[u8], expected_hash: &[u8; 32]) -> bool {
    let actual_hash = hash_data(data);
    actual_hash == *expected_hash
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_key_derivation() {
        let password = "test_password";
        let salt = MasterKey::generate_salt();
        
        let key1 = MasterKey::derive_from_password(password, &salt).unwrap();
        let key2 = MasterKey::derive_from_password(password, &salt).unwrap();
        
        assert_eq!(key1.as_bytes(), key2.as_bytes());
    }

    #[test]
    fn test_encryption_decryption() {
        let password = "test_password";
        let salt = MasterKey::generate_salt();
        let master_key = MasterKey::derive_from_password(password, &salt).unwrap();
        
        let encryptor = ChunkEncryptor::new(&master_key);
        let data = b"Hello, World!";
        
        let (ciphertext, nonce) = encryptor.encrypt_chunk(data).unwrap();
        let decrypted = encryptor.decrypt_chunk(&ciphertext, &nonce).unwrap();
        
        assert_eq!(data, decrypted.as_slice());
    }

    #[test]
    fn test_hashing() {
        let data = b"test data";
        let hash1 = hash_data(data);
        let hash2 = hash_data(data);
        
        assert_eq!(hash1, hash2);
        assert!(verify_hash(data, &hash1));
    }
}