package com.securecloud.android.data.repository

import com.securecloud.android.crypto.CryptoManager
import com.securecloud.android.data.database.ChunkDao
import com.securecloud.android.data.database.ChunkEntity
import com.securecloud.android.data.database.FileDao
import com.securecloud.android.data.database.FileEntity
import com.securecloud.android.data.telegram.TelegramClient
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.withContext
import java.io.File
import java.io.FileOutputStream
import java.util.*
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class SecureCloudRepository @Inject constructor(
    private val fileDao: FileDao,
    private val chunkDao: ChunkDao,
    private val cryptoManager: CryptoManager,
    private val telegramClient: TelegramClient
) {
    private var masterKey: ByteArray? = null

    fun setMasterKey(key: ByteArray) {
        masterKey = key
    }

    fun getAllFiles(): Flow<List<FileEntity>> = fileDao.getAllFiles()

    suspend fun downloadAndDecryptFile(fileId: String, outputFile: File): Result<Unit> = withContext(Dispatchers.IO) {
        try {
            val key = masterKey ?: return@withContext Result.failure(Exception("Master key not set"))
            
            val fileEntity = fileDao.getFileById(fileId)
                ?: return@withContext Result.failure(Exception("File not found"))
            
            val chunks = chunkDao.getChunksForFile(fileId)
            if (chunks.isEmpty()) {
                return@withContext Result.failure(Exception("No chunks found for file"))
            }

            // Download and decrypt chunks
            val decryptedChunks = mutableListOf<ByteArray>()
            
            for (chunk in chunks.sortedBy { it.sequence }) {
                val messageId = chunk.telegramMessageId
                    ?: return@withContext Result.failure(Exception("Chunk not uploaded"))
                
                // Download encrypted chunk from Telegram
                val encryptedData = telegramClient.downloadChunk(messageId)
                    ?: return@withContext Result.failure(Exception("Failed to download chunk"))
                
                // Verify chunk hash
                if (!cryptoManager.verifyHash(encryptedData, chunk.hash)) {
                    return@withContext Result.failure(Exception("Chunk hash verification failed"))
                }
                
                // Decrypt chunk
                val decryptedChunk = cryptoManager.decryptChunk(encryptedData, key, chunk.nonce)
                decryptedChunks.add(decryptedChunk)
            }

            // Combine chunks
            val compressedData = decryptedChunks.fold(ByteArray(0)) { acc, chunk -> acc + chunk }
            
            // Decompress
            val originalData = cryptoManager.decompressData(compressedData)
            
            // Verify file hash
            if (!cryptoManager.verifyHash(originalData, fileEntity.hash)) {
                return@withContext Result.failure(Exception("File hash verification failed"))
            }

            // Write to output file
            FileOutputStream(outputFile).use { it.write(originalData) }
            
            // Mark file as downloaded
            fileDao.updateFile(fileEntity.copy(isDownloaded = true))
            
            Result.success(Unit)
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun syncMetadataFromTelegram(): Result<List<FileEntity>> = withContext(Dispatchers.IO) {
        try {
            // This would typically involve downloading a metadata file from Telegram
            // that contains the file and chunk information
            // For now, return empty list as placeholder
            Result.success(emptyList())
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    suspend fun getFileById(id: String): FileEntity? = fileDao.getFileById(id)

    suspend fun getChunksForFile(fileId: String): List<ChunkEntity> = chunkDao.getChunksForFile(fileId)

    suspend fun insertFile(file: FileEntity) = fileDao.insertFile(file)

    suspend fun insertChunks(chunks: List<ChunkEntity>) = chunkDao.insertChunks(chunks)

    suspend fun deleteFile(fileId: String) {
        fileDao.deleteFileById(fileId)
    }

    suspend fun getSyncStatus(): SyncStatus = withContext(Dispatchers.IO) {
        val allFiles = fileDao.getAllFiles()
        val pendingUploads = chunkDao.getPendingUploads()
        
        // This is a simplified version - in practice you'd collect from Flow
        SyncStatus(
            totalFiles = 0, // Would need to collect from Flow
            totalSize = 0L,
            pendingChunks = pendingUploads.size,
            lastSync = System.currentTimeMillis()
        )
    }
}

data class SyncStatus(
    val totalFiles: Int,
    val totalSize: Long,
    val pendingChunks: Int,
    val lastSync: Long
)