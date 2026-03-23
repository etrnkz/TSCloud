package com.tscloud.android.data.repository

import com.tscloud.android.data.model.FileItem
import com.tscloud.android.data.model.ActivityItem
import com.tscloud.android.data.model.ActivityType
import com.tscloud.android.data.telegram.TelegramClient
import com.tscloud.android.crypto.CryptoManager
import kotlinx.coroutines.delay
import java.util.*
import java.io.File

class TSCloudRepository {
    
    private val _files = mutableListOf<FileItem>()
    private val _activities = mutableListOf<ActivityItem>()
    private val telegramClient = TelegramClient()
    private val cryptoManager = CryptoManager()
    
    init {
        // Add some demo data
        _files.addAll(generateDemoFiles())
        _activities.addAll(generateDemoActivities())
    }
    
    suspend fun getFiles(): List<FileItem> {
        delay(500) // Simulate network delay
        return _files.toList()
    }
    
    suspend fun uploadFile(filePath: String, fileName: String, size: Long, password: String): FileItem {
        try {
            // Read file data
            val fileData = File(filePath).readBytes()
            
            // Encrypt the file
            val encryptionResult = cryptoManager.encryptFile(fileData, password)
            
            // Upload to Telegram
            val uploadResult = telegramClient.uploadFile(
                encryptedData = encryptionResult.encryptedData,
                fileName = fileName,
                originalSize = size,
                encryptedSize = encryptionResult.encryptedData.size.toLong()
            )
            
            val file = FileItem(
                fileName = fileName,
                size = size,
                encryptedSize = encryptionResult.encryptedData.size.toLong(),
                uploadedAt = Date(),
                messageId = uploadResult.messageId,
                fileId = uploadResult.fileId,
                filePath = filePath,
                nonce = encryptionResult.nonce,
                fileHash = encryptionResult.hash,
                isEncrypted = true
            )
            
            _files.add(0, file)
            
            _activities.add(0, ActivityItem(
                type = ActivityType.FILE_UPLOADED,
                title = "File uploaded and encrypted",
                subtitle = fileName
            ))
            
            return file
        } catch (e: Exception) {
            _activities.add(0, ActivityItem(
                type = ActivityType.FILE_UPLOADED, // Using as error activity
                title = "Upload failed",
                subtitle = "Error: ${e.message}"
            ))
            throw e
        }
    }
    
    suspend fun downloadFile(file: FileItem, password: String): ByteArray {
        try {
            // Download encrypted data from Telegram
            val encryptedData = telegramClient.downloadFile(file.messageId, file.fileId)
            
            // Decrypt the file
            val decryptedData = cryptoManager.decryptFile(
                encryptedData = encryptedData,
                nonce = file.nonce,
                password = password,
                expectedHash = file.fileHash
            )
            
            _activities.add(0, ActivityItem(
                type = ActivityType.FILE_DOWNLOADED,
                title = "File downloaded and decrypted",
                subtitle = file.fileName,
                fileId = file.id
            ))
            
            return decryptedData
        } catch (e: Exception) {
            _activities.add(0, ActivityItem(
                type = ActivityType.FILE_DOWNLOADED, // Using as error activity
                title = "Download failed",
                subtitle = "Error: ${e.message}",
                fileId = file.id
            ))
            throw e
        }
    }
    
    suspend fun deleteFile(fileId: String): Boolean {
        try {
            val file = _files.find { it.id == fileId }
            if (file != null) {
                // Delete from Telegram
                telegramClient.deleteFile(file.messageId)
            }
            
            val removed = _files.removeIf { it.id == fileId }
            
            if (removed && file != null) {
                _activities.add(0, ActivityItem(
                    type = ActivityType.FILE_UPLOADED, // Using as generic activity
                    title = "File deleted",
                    subtitle = file.fileName,
                    fileId = fileId
                ))
            }
            
            return removed
        } catch (e: Exception) {
            _activities.add(0, ActivityItem(
                type = ActivityType.FILE_UPLOADED, // Using as error activity
                title = "Delete failed",
                subtitle = "Error: ${e.message}"
            ))
            return false
        }
    }
    
    suspend fun getActivities(): List<ActivityItem> {
        delay(200)
        return _activities.take(50) // Return last 50 activities
    }
    
    suspend fun testConnection(): Boolean {
        return try {
            telegramClient.testConnection()
        } catch (e: Exception) {
            false
        }
    }
    
    private fun generateDemoFiles(): List<FileItem> {
        return listOf(
            FileItem(
                fileName = "presentation.pptx",
                size = 3456789,
                encryptedSize = 3456805,
                uploadedAt = Date(Date().time - 3600000), // 1 hour ago
                messageId = 12345,
                fileId = "file_001",
                isEncrypted = true
            ),
            FileItem(
                fileName = "document.pdf",
                size = 1234567,
                encryptedSize = 1234583,
                uploadedAt = Date(Date().time - 7200000), // 2 hours ago
                messageId = 12346,
                fileId = "file_002",
                isEncrypted = true
            ),
            FileItem(
                fileName = "photo.jpg",
                size = 2345678,
                encryptedSize = 2345694,
                uploadedAt = Date(Date().time - 10800000), // 3 hours ago
                messageId = 12347,
                fileId = "file_003",
                isEncrypted = true,
                isAutoSynced = true
            ),
            FileItem(
                fileName = "spreadsheet.xlsx",
                size = 987654,
                encryptedSize = 987670,
                uploadedAt = Date(Date().time - 14400000), // 4 hours ago
                messageId = 12348,
                fileId = "file_004",
                isEncrypted = true
            ),
            FileItem(
                fileName = "video.mp4",
                size = 15678901,
                encryptedSize = 15678917,
                uploadedAt = Date(Date().time - 18000000), // 5 hours ago
                messageId = 12349,
                fileId = "file_005",
                isEncrypted = true,
                isAutoSynced = true
            )
        )
    }
    
    private fun generateDemoActivities(): List<ActivityItem> {
        return listOf(
            ActivityItem(
                type = ActivityType.FILE_UPLOADED,
                title = "File uploaded",
                subtitle = "presentation.pptx",
                timestamp = Date(Date().time - 1800000) // 30 min ago
            ),
            ActivityItem(
                type = ActivityType.SYNC_COMPLETED,
                title = "Auto-sync completed",
                subtitle = "5 files synced",
                timestamp = Date(Date().time - 3600000) // 1 hour ago
            ),
            ActivityItem(
                type = ActivityType.FILE_ENCRYPTED,
                title = "File encrypted",
                subtitle = "document.pdf",
                timestamp = Date(Date().time - 5400000) // 1.5 hours ago
            ),
            ActivityItem(
                type = ActivityType.FILE_DOWNLOADED,
                title = "File downloaded",
                subtitle = "photo.jpg",
                timestamp = Date(Date().time - 7200000) // 2 hours ago
            ),
            ActivityItem(
                type = ActivityType.FOLDER_ADDED,
                title = "Folder added for sync",
                subtitle = "Documents",
                timestamp = Date(Date().time - 9000000) // 2.5 hours ago
            )
        )
    }
}