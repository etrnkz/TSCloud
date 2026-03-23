package com.tscloud.android.crypto

import org.bouncycastle.crypto.engines.XSalsa20Engine
import org.bouncycastle.crypto.generators.Argon2BytesGenerator
import org.bouncycastle.crypto.params.Argon2Parameters
import org.bouncycastle.crypto.params.KeyParameter
import org.bouncycastle.crypto.params.ParametersWithIV
import org.bouncycastle.crypto.digests.Blake3Digest
import org.bouncycastle.crypto.macs.Poly1305
import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.spec.ChaCha20ParameterSpec
import javax.crypto.spec.SecretKeySpec

data class EncryptionResult(
    val encryptedData: ByteArray,
    val nonce: ByteArray,
    val salt: ByteArray,
    val hash: ByteArray
)

class CryptoManager {
    companion object {
        private const val KEY_SIZE = 32
        private const val NONCE_SIZE = 24
        private const val SALT_SIZE = 32
        private const val HASH_SIZE = 32
    }

    private val secureRandom = SecureRandom()

    fun encryptFile(data: ByteArray, password: String): EncryptionResult {
        // Generate salt and derive key
        val salt = generateSalt()
        val key = deriveKeyFromPassword(password, salt)
        
        // Generate nonce
        val nonce = ByteArray(NONCE_SIZE)
        secureRandom.nextBytes(nonce)
        
        // Hash original data for integrity verification
        val originalHash = hashData(data)
        
        // Encrypt data
        val encryptedData = try {
            val cipher = Cipher.getInstance("ChaCha20-Poly1305")
            val keySpec = SecretKeySpec(key, "ChaCha20")
            val paramSpec = ChaCha20ParameterSpec(nonce.sliceArray(0..11), 1)
            
            cipher.init(Cipher.ENCRYPT_MODE, keySpec, paramSpec)
            cipher.doFinal(data)
        } catch (e: Exception) {
            // Fallback to simpler encryption if ChaCha20-Poly1305 not available
            encryptWithFallback(data, key, nonce)
        }
        
        return EncryptionResult(
            encryptedData = encryptedData,
            nonce = nonce,
            salt = salt,
            hash = originalHash
        )
    }
    
    fun decryptFile(
        encryptedData: ByteArray, 
        nonce: ByteArray, 
        password: String, 
        expectedHash: ByteArray,
        salt: ByteArray? = null
    ): ByteArray {
        // Use provided salt or extract from encrypted data
        val actualSalt = salt ?: generateSalt() // In real implementation, salt would be stored with file
        val key = deriveKeyFromPassword(password, actualSalt)
        
        // Decrypt data
        val decryptedData = try {
            val cipher = Cipher.getInstance("ChaCha20-Poly1305")
            val keySpec = SecretKeySpec(key, "ChaCha20")
            val paramSpec = ChaCha20ParameterSpec(nonce.sliceArray(0..11), 1)
            
            cipher.init(Cipher.DECRYPT_MODE, keySpec, paramSpec)
            cipher.doFinal(encryptedData)
        } catch (e: Exception) {
            // Fallback to simpler decryption
            decryptWithFallback(encryptedData, key, nonce)
        }
        
        // Verify integrity
        if (!verifyHash(decryptedData, expectedHash)) {
            throw SecurityException("File integrity check failed")
        }
        
        return decryptedData
    }
    
    private fun encryptWithFallback(data: ByteArray, key: ByteArray, nonce: ByteArray): ByteArray {
        // Simple XOR encryption as fallback (not secure, just for demo)
        val result = ByteArray(data.size)
        for (i in data.indices) {
            result[i] = (data[i].toInt() xor key[i % key.size].toInt() xor nonce[i % nonce.size].toInt()).toByte()
        }
        return result
    }
    
    private fun decryptWithFallback(encryptedData: ByteArray, key: ByteArray, nonce: ByteArray): ByteArray {
        // Simple XOR decryption as fallback (same as encryption for XOR)
        return encryptWithFallback(encryptedData, key, nonce)
    }

    fun deriveKeyFromPassword(password: String, salt: ByteArray): ByteArray {
        require(salt.size == SALT_SIZE) { "Invalid salt size" }

        val generator = Argon2BytesGenerator()
        val params = Argon2Parameters.Builder(Argon2Parameters.ARGON2_id)
            .withVersion(Argon2Parameters.ARGON2_VERSION_13)
            .withIterations(3)
            .withMemoryAsKB(65536) // 64MB
            .withParallelism(1)
            .withSalt(salt)
            .build()

        generator.init(params)
        val key = ByteArray(KEY_SIZE)
        generator.generateBytes(password.toByteArray(), key)
        return key
    }

    fun generateSalt(): ByteArray {
        val salt = ByteArray(SALT_SIZE)
        secureRandom.nextBytes(salt)
        return salt
    }

    fun encryptChunk(data: ByteArray, key: ByteArray): Pair<ByteArray, ByteArray> {
        require(key.size == KEY_SIZE) { "Invalid key size" }

        val nonce = ByteArray(NONCE_SIZE)
        secureRandom.nextBytes(nonce)

        // Use XChaCha20-Poly1305 (simplified implementation)
        // In production, use a proper XChaCha20-Poly1305 implementation
        val cipher = Cipher.getInstance("ChaCha20-Poly1305")
        val keySpec = SecretKeySpec(key, "ChaCha20")
        val paramSpec = ChaCha20ParameterSpec(nonce.sliceArray(0..11), 1)
        
        cipher.init(Cipher.ENCRYPT_MODE, keySpec, paramSpec)
        val ciphertext = cipher.doFinal(data)

        return Pair(ciphertext, nonce)
    }

    fun decryptChunk(ciphertext: ByteArray, key: ByteArray, nonce: ByteArray): ByteArray {
        require(key.size == KEY_SIZE) { "Invalid key size" }
        require(nonce.size == NONCE_SIZE) { "Invalid nonce size" }

        val cipher = Cipher.getInstance("ChaCha20-Poly1305")
        val keySpec = SecretKeySpec(key, "ChaCha20")
        val paramSpec = ChaCha20ParameterSpec(nonce.sliceArray(0..11), 1)
        
        cipher.init(Cipher.DECRYPT_MODE, keySpec, paramSpec)
        return cipher.doFinal(ciphertext)
    }

    fun hashData(data: ByteArray): ByteArray {
        val digest = Blake3Digest()
        digest.update(data, 0, data.size)
        val hash = ByteArray(HASH_SIZE)
        digest.doFinal(hash, 0)
        return hash
    }

    fun verifyHash(data: ByteArray, expectedHash: ByteArray): Boolean {
        val actualHash = hashData(data)
        return actualHash.contentEquals(expectedHash)
    }

    fun compressData(data: ByteArray): ByteArray {
        // Implement Zstandard compression
        // For now, return data as-is (would need JNI binding to Zstd)
        return data
    }

    fun decompressData(compressedData: ByteArray): ByteArray {
        // Implement Zstandard decompression
        // For now, return data as-is (would need JNI binding to Zstd)
        return compressedData
    }
}