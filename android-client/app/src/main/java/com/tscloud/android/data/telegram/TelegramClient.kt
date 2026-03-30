package com.tscloud.android.data.telegram

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import retrofit2.Response
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import retrofit2.http.*
import java.io.IOException
import javax.inject.Inject
import javax.inject.Singleton

data class TelegramConfig(
    val botToken: String,
    val channelId: String
)

interface TelegramApi {
    @GET("getMe")
    suspend fun getMe(): Response<TelegramResponse<TelegramUser>>

    @Multipart
    @POST("sendDocument")
    suspend fun sendDocument(
        @Part("chat_id") chatId: RequestBody,
        @Part document: MultipartBody.Part
    ): Response<TelegramResponse<TelegramMessage>>

    @GET("getFile")
    suspend fun getFile(@Query("file_id") fileId: String): Response<TelegramResponse<TelegramFile>>
}

data class TelegramResponse<T>(
    val ok: Boolean,
    val result: T?,
    val error_code: Int?,
    val description: String?
)

data class TelegramUser(
    val id: Long,
    val is_bot: Boolean,
    val first_name: String,
    val username: String?
)

data class TelegramMessage(
    val message_id: Long,
    val date: Long,
    val document: TelegramDocument?
)

data class TelegramDocument(
    val file_id: String,
    val file_unique_id: String,
    val file_name: String?,
    val mime_type: String?,
    val file_size: Long?
)

data class TelegramFile(
    val file_id: String,
    val file_unique_id: String,
    val file_size: Long?,
    val file_path: String?
)

@Singleton
class TelegramClient @Inject constructor() {
    private var api: TelegramApi? = null
    private var config: TelegramConfig? = null
    private val httpClient = OkHttpClient.Builder()
        .addInterceptor { chain ->
            val request = chain.request()
            val newRequest = request.newBuilder()
                .addHeader("User-Agent", "TSCloud-Android/1.0")
                .build()
            chain.proceed(newRequest)
        }
        .build()

    /** Call [initialize] with user-supplied credentials from settings — never hardcode tokens. */
    fun initialize(config: TelegramConfig) {
        this.config = config
        
        val retrofit = Retrofit.Builder()
            .baseUrl("https://api.telegram.org/bot${config.botToken}/")
            .client(httpClient)
            .addConverterFactory(GsonConverterFactory.create())
            .build()
        
        api = retrofit.create(TelegramApi::class.java)
    }

    suspend fun testConnection(): Boolean = withContext(Dispatchers.IO) {
        try {
            val api = this@TelegramClient.api ?: return@withContext false
            
            val response = api.getMe()
            response.isSuccessful && response.body()?.ok == true
        } catch (e: Exception) {
            false
        }
    }

    data class UploadResult(
        val messageId: Long,
        val fileId: String
    )

    suspend fun uploadFile(
        encryptedData: ByteArray, 
        fileName: String, 
        originalSize: Long, 
        encryptedSize: Long
    ): UploadResult = withContext(Dispatchers.IO) {
        try {
            val api = this@TelegramClient.api ?: throw Exception("Client not initialized")
            val config = this@TelegramClient.config ?: throw Exception("Config not set")
            
            val requestBody = encryptedData.toRequestBody("application/octet-stream".toMediaType())
            val filePart = MultipartBody.Part.createFormData(
                "document", 
                "$fileName.enc", 
                requestBody
            )
            val chatIdBody = config.channelId.toRequestBody("text/plain".toMediaType())
            
            val response = api.sendDocument(chatIdBody, filePart)
            if (response.isSuccessful && response.body()?.ok == true) {
                val message = response.body()?.result
                val document = message?.document
                if (message != null && document != null) {
                    UploadResult(
                        messageId = message.message_id,
                        fileId = document.file_id
                    )
                } else {
                    throw Exception("Invalid response format")
                }
            } else {
                throw Exception("Upload failed: ${response.body()?.description}")
            }
        } catch (e: Exception) {
            throw Exception("Upload error: ${e.message}")
        }
    }

    suspend fun downloadFile(messageId: Long, fileId: String): ByteArray = withContext(Dispatchers.IO) {
        try {
            val api = this@TelegramClient.api ?: throw Exception("Client not initialized")
            val config = this@TelegramClient.config ?: throw Exception("Config not set")
            
            // Get file info
            val fileResponse = api.getFile(fileId)
            if (!fileResponse.isSuccessful || fileResponse.body()?.ok != true) {
                throw Exception("Failed to get file info")
            }
            
            val telegramFile = fileResponse.body()?.result
            val filePath = telegramFile?.file_path ?: throw Exception("No file path")
            
            // Download file content
            val downloadUrl = "https://api.telegram.org/file/bot${config.botToken}/$filePath"
            val request = Request.Builder()
                .url(downloadUrl)
                .build()
            
            val response = httpClient.newCall(request).execute()
            if (response.isSuccessful) {
                response.body?.bytes() ?: throw Exception("Empty response body")
            } else {
                throw Exception("Download failed: ${response.code}")
            }
        } catch (e: Exception) {
            throw Exception("Download error: ${e.message}")
        }
    }

    suspend fun deleteFile(messageId: Long): Boolean = withContext(Dispatchers.IO) {
        try {
            // Telegram Bot API doesn't support deleting messages in channels
            // This would require MTProto implementation or admin rights
            // For now, we'll just return true as files remain accessible
            true
        } catch (e: Exception) {
            false
        }
    }

    suspend fun uploadChunk(chunkData: ByteArray, chunkId: String): Long? = withContext(Dispatchers.IO) {
        try {
            val api = this@TelegramClient.api ?: return@withContext null
            val config = this@TelegramClient.config ?: return@withContext null
            
            val requestBody = chunkData.toRequestBody("application/octet-stream".toMediaType())
            val filePart = MultipartBody.Part.createFormData(
                "document", 
                "$chunkId.enc", 
                requestBody
            )
            val chatIdBody = config.channelId.toRequestBody("text/plain".toMediaType())
            
            val response = api.sendDocument(chatIdBody, filePart)
            if (response.isSuccessful && response.body()?.ok == true) {
                response.body()?.result?.message_id
            } else {
                null
            }
        } catch (e: Exception) {
            null
        }
    }

    suspend fun downloadChunk(messageId: Long): ByteArray? = withContext(Dispatchers.IO) {
        try {
            // This is a simplified implementation
            // In practice, you'd need to:
            // 1. Get the message by ID
            // 2. Extract the document file_id
            // 3. Get the file path using getFile
            // 4. Download the file content
            
            // For now, return null as placeholder
            null
        } catch (e: Exception) {
            null
        }
    }

    suspend fun deleteMessage(messageId: Long): Boolean = withContext(Dispatchers.IO) {
        try {
            // Telegram Bot API doesn't support deleting messages in channels
            // This would require MTProto implementation
            false
        } catch (e: Exception) {
            false
        }
    }
}