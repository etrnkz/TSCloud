package com.securecloud.android.ui.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import com.securecloud.android.data.model.FileItem
import com.securecloud.android.data.repository.SecureCloudRepository
import java.util.*

class FileListViewModel(
    private val repository: SecureCloudRepository = SecureCloudRepository()
) : ViewModel() {
    
    private val _files = MutableStateFlow<List<FileItem>>(emptyList())
    val files: StateFlow<List<FileItem>> = _files.asStateFlow()
    
    private val _isLoading = MutableStateFlow(false)
    val isLoading: StateFlow<Boolean> = _isLoading.asStateFlow()
    
    private val _uploadProgress = MutableStateFlow<Map<String, Float>>(emptyMap())
    val uploadProgress: StateFlow<Map<String, Float>> = _uploadProgress.asStateFlow()
    
    private val _downloadProgress = MutableStateFlow<Map<String, Float>>(emptyMap())
    val downloadProgress: StateFlow<Map<String, Float>> = _downloadProgress.asStateFlow()
    
    init {
        loadFiles()
    }
    
    fun loadFiles() {
        viewModelScope.launch {
            _isLoading.value = true
            try {
                val fileList = repository.getFiles()
                _files.value = fileList
            } catch (e: Exception) {
                // Handle error
                println("Error loading files: ${e.message}")
            } finally {
                _isLoading.value = false
            }
        }
    }
    
    fun addFile() {
        viewModelScope.launch {
            try {
                // TODO: Implement file picker
                // For now, create a demo file
                val demoFile = FileItem(
                    fileName = "demo_${Date().time}.txt",
                    size = 1024,
                    encryptedSize = 1040,
                    isEncrypted = true,
                    isUploading = true,
                    progress = 0f
                )
                
                val currentFiles = _files.value.toMutableList()
                currentFiles.add(0, demoFile)
                _files.value = currentFiles
                
                // Simulate upload progress
                simulateUploadProgress(demoFile.id)
                
                // TODO: Get password from user and call repository.uploadFile()
                // val password = getPasswordFromUser()
                // repository.uploadFile(filePath, fileName, size, password)
                
            } catch (e: Exception) {
                println("Error adding file: ${e.message}")
            }
        }
    }
    
    fun downloadFile(file: FileItem) {
        viewModelScope.launch {
            try {
                // Update file state to downloading
                val updatedFiles = _files.value.map { 
                    if (it.id == file.id) it.copy(isDownloading = true, progress = 0f) 
                    else it 
                }
                _files.value = updatedFiles
                
                // Simulate download progress
                simulateDownloadProgress(file.id)
                
                // TODO: Get password from user and call repository.downloadFile()
                // val password = getPasswordFromUser()
                // repository.downloadFile(file, password)
                
            } catch (e: Exception) {
                println("Error downloading file: ${e.message}")
            }
        }
    }
    
    fun deleteFile(file: FileItem) {
        viewModelScope.launch {
            try {
                repository.deleteFile(file.id)
                val updatedFiles = _files.value.filter { it.id != file.id }
                _files.value = updatedFiles
            } catch (e: Exception) {
                println("Error deleting file: ${e.message}")
            }
        }
    }
    
    private suspend fun simulateUploadProgress(fileId: String) {
        for (progress in 0..100 step 10) {
            kotlinx.coroutines.delay(200)
            val updatedFiles = _files.value.map { 
                if (it.id == fileId) it.copy(progress = progress / 100f) 
                else it 
            }
            _files.value = updatedFiles
        }
        
        // Mark as completed
        val completedFiles = _files.value.map { 
            if (it.id == fileId) it.copy(isUploading = false, progress = 1f) 
            else it 
        }
        _files.value = completedFiles
    }
    
    private suspend fun simulateDownloadProgress(fileId: String) {
        for (progress in 0..100 step 15) {
            kotlinx.coroutines.delay(150)
            val updatedFiles = _files.value.map { 
                if (it.id == fileId) it.copy(progress = progress / 100f) 
                else it 
            }
            _files.value = updatedFiles
        }
        
        // Mark as completed
        val completedFiles = _files.value.map { 
            if (it.id == fileId) it.copy(isDownloading = false, progress = 1f) 
            else it 
        }
        _files.value = completedFiles
    }
}