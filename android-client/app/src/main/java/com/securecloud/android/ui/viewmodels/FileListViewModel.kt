package com.securecloud.android.ui.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.securecloud.android.data.database.FileEntity
import com.securecloud.android.data.repository.SecureCloudRepository
import com.securecloud.android.data.repository.SyncStatus
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.launch
import java.io.File
import javax.inject.Inject

data class FileListUiState(
    val files: List<FileEntity> = emptyList(),
    val syncStatus: SyncStatus? = null,
    val isLoading: Boolean = false,
    val error: String? = null
)

@HiltViewModel
class FileListViewModel @Inject constructor(
    private val repository: SecureCloudRepository
) : ViewModel() {
    
    private val _uiState = MutableStateFlow(FileListUiState())
    val uiState: StateFlow<FileListUiState> = _uiState.asStateFlow()
    
    fun loadFiles() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            
            try {
                repository.getAllFiles()
                    .catch { e ->
                        _uiState.value = _uiState.value.copy(
                            isLoading = false,
                            error = "Failed to load files: ${e.message}"
                        )
                    }
                    .collect { files ->
                        _uiState.value = _uiState.value.copy(
                            files = files,
                            isLoading = false
                        )
                    }
                
                // Also load sync status
                val syncStatus = repository.getSyncStatus()
                _uiState.value = _uiState.value.copy(syncStatus = syncStatus)
                
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    error = "Failed to load files: ${e.message}"
                )
            }
        }
    }
    
    fun refreshFiles() {
        loadFiles()
    }
    
    fun syncFromTelegram() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            
            try {
                val result = repository.syncMetadataFromTelegram()
                result.fold(
                    onSuccess = { files ->
                        _uiState.value = _uiState.value.copy(
                            isLoading = false,
                            error = if (files.isEmpty()) "No new files found" else null
                        )
                        // Refresh the file list
                        loadFiles()
                    },
                    onFailure = { e ->
                        _uiState.value = _uiState.value.copy(
                            isLoading = false,
                            error = "Sync failed: ${e.message}"
                        )
                    }
                )
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    error = "Sync failed: ${e.message}"
                )
            }
        }
    }
    
    fun downloadFile(fileId: String) {
        viewModelScope.launch {
            try {
                val file = repository.getFileById(fileId)
                if (file == null) {
                    _uiState.value = _uiState.value.copy(error = "File not found")
                    return@launch
                }
                
                // Create output file in app's private directory
                val outputDir = File("/data/data/com.securecloud.android/files/downloads")
                if (!outputDir.exists()) {
                    outputDir.mkdirs()
                }
                val outputFile = File(outputDir, file.name)
                
                val result = repository.downloadAndDecryptFile(fileId, outputFile)
                result.fold(
                    onSuccess = {
                        _uiState.value = _uiState.value.copy(
                            error = "File downloaded successfully to ${outputFile.absolutePath}"
                        )
                        // Refresh to show updated download status
                        loadFiles()
                    },
                    onFailure = { e ->
                        _uiState.value = _uiState.value.copy(
                            error = "Download failed: ${e.message}"
                        )
                    }
                )
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(
                    error = "Download failed: ${e.message}"
                )
            }
        }
    }
    
    fun clearError() {
        _uiState.value = _uiState.value.copy(error = null)
    }
}