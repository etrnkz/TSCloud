package com.securecloud.android.ui.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.securecloud.android.crypto.CryptoManager
import com.securecloud.android.data.repository.SecureCloudRepository
import com.securecloud.android.security.KeystoreManager
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

data class LoginUiState(
    val isLoading: Boolean = false,
    val isLoggedIn: Boolean = false,
    val error: String? = null
)

@HiltViewModel
class LoginViewModel @Inject constructor(
    private val repository: SecureCloudRepository,
    private val cryptoManager: CryptoManager,
    private val keystoreManager: KeystoreManager
) : ViewModel() {
    
    private val _uiState = MutableStateFlow(LoginUiState())
    val uiState: StateFlow<LoginUiState> = _uiState.asStateFlow()
    
    fun login(password: String) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            
            try {
                // Try to get stored salt, or generate new one
                val salt = keystoreManager.getSalt() ?: run {
                    val newSalt = cryptoManager.generateSalt()
                    keystoreManager.storeSalt(newSalt)
                    newSalt
                }
                
                // Derive master key from password
                val masterKey = cryptoManager.deriveKeyFromPassword(password, salt)
                
                // Store master key securely (encrypted with Android Keystore)
                keystoreManager.storeMasterKey(masterKey)
                
                // Set master key in repository
                repository.setMasterKey(masterKey)
                
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    isLoggedIn = true
                )
                
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    error = "Login failed: ${e.message}"
                )
            }
        }
    }
    
    fun clearError() {
        _uiState.value = _uiState.value.copy(error = null)
    }
}