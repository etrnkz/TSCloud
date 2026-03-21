package com.securecloud.android.data.model

import java.util.*

data class FileItem(
    val id: String = UUID.randomUUID().toString(),
    val fileName: String,
    val size: Long,
    val encryptedSize: Long = size,
    val uploadedAt: Date = Date(),
    val messageId: Long = 0,
    val fileId: String = "",
    val filePath: String = "",
    val nonce: ByteArray = byteArrayOf(),
    val fileHash: ByteArray = byteArrayOf(),
    val isEncrypted: Boolean = nonce.isNotEmpty(),
    val isUploading: Boolean = false,
    val isDownloading: Boolean = false,
    val progress: Float = 0f,
    val isAutoSynced: Boolean = false
) {
    override fun equals(other: Any?): Boolean {
        if (this === other) return true
        if (javaClass != other?.javaClass) return false

        other as FileItem

        if (id != other.id) return false
        if (fileName != other.fileName) return false
        if (size != other.size) return false
        if (messageId != other.messageId) return false

        return true
    }

    override fun hashCode(): Int {
        var result = id.hashCode()
        result = 31 * result + fileName.hashCode()
        result = 31 * result + size.hashCode()
        result = 31 * result + messageId.hashCode()
        return result
    }
}

data class SyncFolder(
    val id: String = UUID.randomUUID().toString(),
    val folderPath: String,
    val folderName: String = folderPath.substringAfterLast('/'),
    val isActive: Boolean = true,
    val fileCount: Int = 0,
    val totalSize: Long = 0,
    val lastSync: Date? = null,
    val addedAt: Date = Date()
)

data class ActivityItem(
    val id: String = UUID.randomUUID().toString(),
    val type: ActivityType,
    val title: String,
    val subtitle: String,
    val timestamp: Date = Date(),
    val fileId: String? = null
)

enum class ActivityType {
    FILE_UPLOADED,
    FILE_DOWNLOADED,
    FILE_ENCRYPTED,
    FILE_DECRYPTED,
    SYNC_STARTED,
    SYNC_COMPLETED,
    FOLDER_ADDED,
    FOLDER_REMOVED,
    ERROR
}