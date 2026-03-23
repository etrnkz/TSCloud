package com.tscloud.android.data.database

import androidx.room.*
import kotlinx.coroutines.flow.Flow

@Entity(tableName = "files")
data class FileEntity(
    @PrimaryKey val id: String,
    val path: String,
    val name: String,
    val size: Long,
    val hash: ByteArray,
    val compressedSize: Long,
    val chunkCount: Int,
    val createdAt: Long,
    val updatedAt: Long,
    val isDownloaded: Boolean = false
) {
    override fun equals(other: Any?): Boolean {
        if (this === other) return true
        if (javaClass != other?.javaClass) return false

        other as FileEntity

        if (id != other.id) return false
        if (!hash.contentEquals(other.hash)) return false

        return true
    }

    override fun hashCode(): Int {
        var result = id.hashCode()
        result = 31 * result + hash.contentHashCode()
        return result
    }
}

@Entity(
    tableName = "chunks",
    foreignKeys = [ForeignKey(
        entity = FileEntity::class,
        parentColumns = ["id"],
        childColumns = ["fileId"],
        onDelete = ForeignKey.CASCADE
    )],
    indices = [Index("fileId"), Index("fileId", "sequence")]
)
data class ChunkEntity(
    @PrimaryKey val id: String,
    val fileId: String,
    val sequence: Int,
    val size: Int,
    val compressedSize: Int,
    val hash: ByteArray,
    val nonce: ByteArray,
    val telegramMessageId: Long?,
    val uploadedAt: Long?,
    val isDownloaded: Boolean = false
) {
    override fun equals(other: Any?): Boolean {
        if (this === other) return true
        if (javaClass != other?.javaClass) return false

        other as ChunkEntity

        if (id != other.id) return false
        if (!hash.contentEquals(other.hash)) return false
        if (!nonce.contentEquals(other.nonce)) return false

        return true
    }

    override fun hashCode(): Int {
        var result = id.hashCode()
        result = 31 * result + hash.contentHashCode()
        result = 31 * result + nonce.contentHashCode()
        return result
    }
}

@Dao
interface FileDao {
    @Query("SELECT * FROM files ORDER BY name")
    fun getAllFiles(): Flow<List<FileEntity>>

    @Query("SELECT * FROM files WHERE id = :id")
    suspend fun getFileById(id: String): FileEntity?

    @Query("SELECT * FROM files WHERE path = :path")
    suspend fun getFileByPath(path: String): FileEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertFile(file: FileEntity)

    @Update
    suspend fun updateFile(file: FileEntity)

    @Delete
    suspend fun deleteFile(file: FileEntity)

    @Query("DELETE FROM files WHERE id = :id")
    suspend fun deleteFileById(id: String)
}

@Dao
interface ChunkDao {
    @Query("SELECT * FROM chunks WHERE fileId = :fileId ORDER BY sequence")
    suspend fun getChunksForFile(fileId: String): List<ChunkEntity>

    @Query("SELECT * FROM chunks WHERE id = :id")
    suspend fun getChunkById(id: String): ChunkEntity?

    @Query("SELECT * FROM chunks WHERE telegramMessageId IS NULL ORDER BY fileId, sequence")
    suspend fun getPendingUploads(): List<ChunkEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertChunk(chunk: ChunkEntity)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertChunks(chunks: List<ChunkEntity>)

    @Update
    suspend fun updateChunk(chunk: ChunkEntity)

    @Query("UPDATE chunks SET telegramMessageId = :messageId, uploadedAt = :uploadedAt WHERE id = :chunkId")
    suspend fun updateChunkTelegramInfo(chunkId: String, messageId: Long, uploadedAt: Long)

    @Delete
    suspend fun deleteChunk(chunk: ChunkEntity)
}

@Database(
    entities = [FileEntity::class, ChunkEntity::class],
    version = 1,
    exportSchema = false
)
@TypeConverters(Converters::class)
abstract class TSCloudDatabase : RoomDatabase() {
    abstract fun fileDao(): FileDao
    abstract fun chunkDao(): ChunkDao
}

class Converters {
    @TypeConverter
    fun fromByteArray(value: ByteArray): String {
        return android.util.Base64.encodeToString(value, android.util.Base64.DEFAULT)
    }

    @TypeConverter
    fun toByteArray(value: String): ByteArray {
        return android.util.Base64.decode(value, android.util.Base64.DEFAULT)
    }
}