package com.securecloud.android.security

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey
import dagger.hilt.android.qualifiers.ApplicationContext
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class KeystoreManager @Inject constructor(
    @ApplicationContext private val context: Context
) {
    companion object {
        private const val KEYSTORE_ALIAS = "SecureCloudMasterKey"
        private const val SALT_KEY = "master_key_salt"
        private const val ENCRYPTED_KEY = "encrypted_master_key"
        private const val IV_KEY = "master_key_iv"
        private const val PREFS_NAME = "secure_cloud_prefs"
    }

    private val keyStore: KeyStore = KeyStore.getInstance("AndroidKeyStore").apply {
        load(null)
    }

    private val encryptedPrefs by lazy {
        val masterKey = MasterKey.Builder(context)
            .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
            .build()

        EncryptedSharedPreferences.create(
            context,
            PREFS_NAME,
            masterKey,
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
        )
    }

    init {
        generateOrGetSecretKey()
    }

    private fun generateOrGetSecretKey(): SecretKey {
        return if (keyStore.containsAlias(KEYSTORE_ALIAS)) {
            keyStore.getKey(KEYSTORE_ALIAS, null) as SecretKey
        } else {
            val keyGenerator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
            val keyGenParameterSpec = KeyGenParameterSpec.Builder(
                KEYSTORE_ALIAS,
                KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT
            )
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setUserAuthenticationRequired(false) // Set to true for biometric authentication
                .build()

            keyGenerator.init(keyGenParameterSpec)
            keyGenerator.generateKey()
        }
    }

    fun storeSalt(salt: ByteArray) {
        val saltBase64 = android.util.Base64.encodeToString(salt, android.util.Base64.DEFAULT)
        encryptedPrefs.edit()
            .putString(SALT_KEY, saltBase64)
            .apply()
    }

    fun getSalt(): ByteArray? {
        val saltBase64 = encryptedPrefs.getString(SALT_KEY, null)
        return saltBase64?.let { 
            android.util.Base64.decode(it, android.util.Base64.DEFAULT)
        }
    }

    fun storeMasterKey(masterKey: ByteArray) {
        try {
            val secretKey = keyStore.getKey(KEYSTORE_ALIAS, null) as SecretKey
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.ENCRYPT_MODE, secretKey)

            val encryptedKey = cipher.doFinal(masterKey)
            val iv = cipher.iv

            val encryptedKeyBase64 = android.util.Base64.encodeToString(encryptedKey, android.util.Base64.DEFAULT)
            val ivBase64 = android.util.Base64.encodeToString(iv, android.util.Base64.DEFAULT)

            encryptedPrefs.edit()
                .putString(ENCRYPTED_KEY, encryptedKeyBase64)
                .putString(IV_KEY, ivBase64)
                .apply()

        } catch (e: Exception) {
            throw SecurityException("Failed to store master key", e)
        }
    }

    fun getMasterKey(): ByteArray? {
        return try {
            val encryptedKeyBase64 = encryptedPrefs.getString(ENCRYPTED_KEY, null) ?: return null
            val ivBase64 = encryptedPrefs.getString(IV_KEY, null) ?: return null

            val encryptedKey = android.util.Base64.decode(encryptedKeyBase64, android.util.Base64.DEFAULT)
            val iv = android.util.Base64.decode(ivBase64, android.util.Base64.DEFAULT)

            val secretKey = keyStore.getKey(KEYSTORE_ALIAS, null) as SecretKey
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            val spec = GCMParameterSpec(128, iv)
            cipher.init(Cipher.DECRYPT_MODE, secretKey, spec)

            cipher.doFinal(encryptedKey)

        } catch (e: Exception) {
            null
        }
    }

    fun clearStoredKeys() {
        encryptedPrefs.edit()
            .remove(SALT_KEY)
            .remove(ENCRYPTED_KEY)
            .remove(IV_KEY)
            .apply()
    }

    fun hasMasterKey(): Boolean {
        return encryptedPrefs.contains(ENCRYPTED_KEY) && encryptedPrefs.contains(IV_KEY)
    }
}