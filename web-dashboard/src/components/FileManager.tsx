'use client'

import { useState, useEffect } from 'react'
import { motion } from 'framer-motion'
import {
  DocumentIcon,
  CloudArrowUpIcon,
  CloudArrowDownIcon,
  TrashIcon,
  EyeIcon,
  LockClosedIcon,
  FolderIcon,
  MagnifyingGlassIcon
} from '@heroicons/react/24/outline'

interface FileItem {
  id: string
  name: string
  size: number
  encryptedSize: number
  uploadedAt: Date
  isEncrypted: boolean
  type: string
  messageId: number
}

export default function FileManager() {
  const [files, setFiles] = useState<FileItem[]>([])
  const [searchTerm, setSearchTerm] = useState('')
  const [selectedFiles, setSelectedFiles] = useState<string[]>([])
  const [isUploading, setIsUploading] = useState(false)
  const [uploadProgress, setUploadProgress] = useState(0)

  useEffect(() => {
    // Load demo files
    setFiles([
      {
        id: '1',
        name: 'presentation.pptx',
        size: 3456789,
        encryptedSize: 3456805,
        uploadedAt: new Date(Date.now() - 3600000),
        isEncrypted: true,
        type: 'application/vnd.openxmlformats-officedocument.presentationml.presentation',
        messageId: 12345
      },
      {
        id: '2',
        name: 'document.pdf',
        size: 1234567,
        encryptedSize: 1234583,
        uploadedAt: new Date(Date.now() - 7200000),
        isEncrypted: true,
        type: 'application/pdf',
        messageId: 12346
      },
      {
        id: '3',
        name: 'photo.jpg',
        size: 2345678,
        encryptedSize: 2345694,
        uploadedAt: new Date(Date.now() - 10800000),
        isEncrypted: true,
        type: 'image/jpeg',
        messageId: 12347
      },
      {
        id: '4',
        name: 'spreadsheet.xlsx',
        size: 987654,
        encryptedSize: 987670,
        uploadedAt: new Date(Date.now() - 14400000),
        isEncrypted: true,
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        messageId: 12348
      }
    ])
  }, [])

  const filteredFiles = files.filter(file =>
    file.name.toLowerCase().includes(searchTerm.toLowerCase())
  )

  const formatFileSize = (bytes: number) => {
    if (bytes === 0) return '0 Bytes'
    const k = 1024
    const sizes = ['Bytes', 'KB', 'MB', 'GB']
    const i = Math.floor(Math.log(bytes) / Math.log(k))
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
  }

  const getFileIcon = (type: string) => {
    if (type.startsWith('image/')) return '🖼️'
    if (type.includes('pdf')) return '📄'
    if (type.includes('presentation')) return '📊'
    if (type.includes('spreadsheet')) return '📈'
    if (type.includes('document')) return '📝'
    return '📄'
  }

  const handleFileUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file) return

    setIsUploading(true)
    setUploadProgress(0)

    // Simulate upload progress
    const interval = setInterval(() => {
      setUploadProgress(prev => {
        if (prev >= 100) {
          clearInterval(interval)
          setIsUploading(false)
          
          // Add file to list
          const newFile: FileItem = {
            id: Date.now().toString(),
            name: file.name,
            size: file.size,
            encryptedSize: file.size + 16, // Add encryption overhead
            uploadedAt: new Date(),
            isEncrypted: true,
            type: file.type,
            messageId: Math.floor(Math.random() * 100000)
          }
          setFiles(prev => [newFile, ...prev])
          
          return 0
        }
        return prev + 10
      })
    }, 200)
  }

  const handleDownload = async (file: FileItem) => {
    // TODO: Implement actual download
    console.log('Downloading file:', file.name)
  }

  const handleDelete = async (fileId: string) => {
    if (confirm('Are you sure you want to delete this file?')) {
      setFiles(prev => prev.filter(f => f.id !== fileId))
    }
  }

  const toggleFileSelection = (fileId: string) => {
    setSelectedFiles(prev =>
      prev.includes(fileId)
        ? prev.filter(id => id !== fileId)
        : [...prev, fileId]
    )
  }

  const handleBulkDelete = async () => {
    if (selectedFiles.length === 0) return
    if (confirm(`Are you sure you want to delete ${selectedFiles.length} files?`)) {
      setFiles(prev => prev.filter(f => !selectedFiles.includes(f.id)))
      setSelectedFiles([])
    }
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex justify-between items-center">
        <h2 className="text-2xl font-bold text-gray-900 dark:text-white">
          File Manager
        </h2>
        <div className="flex items-center space-x-4">
          {selectedFiles.length > 0 && (
            <button
              onClick={handleBulkDelete}
              className="px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors flex items-center"
            >
              <TrashIcon className="h-4 w-4 mr-2" />
              Delete {selectedFiles.length} files
            </button>
          )}
          <label className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors cursor-pointer flex items-center">
            <CloudArrowUpIcon className="h-4 w-4 mr-2" />
            Upload File
            <input
              type="file"
              className="hidden"
              onChange={handleFileUpload}
              disabled={isUploading}
            />
          </label>
        </div>
      </div>

      {/* Search */}
      <div className="relative">
        <MagnifyingGlassIcon className="h-5 w-5 absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />
        <input
          type="text"
          placeholder="Search files..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          className="w-full pl-10 pr-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
        />
      </div>

      {/* Upload Progress */}
      {isUploading && (
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-4">
          <div className="flex items-center justify-between mb-2">
            <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Uploading and encrypting...
            </span>
            <span className="text-sm text-gray-500 dark:text-gray-400">
              {uploadProgress}%
            </span>
          </div>
          <div className="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-2">
            <div
              className="bg-blue-600 h-2 rounded-full transition-all duration-300"
              style={{ width: `${uploadProgress}%` }}
            />
          </div>
        </div>
      )}

      {/* Files Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
        {filteredFiles.map((file) => (
          <motion.div
            key={file.id}
            initial={{ opacity: 0, scale: 0.9 }}
            animate={{ opacity: 1, scale: 1 }}
            className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-4 hover:shadow-md transition-shadow"
          >
            <div className="flex items-start justify-between mb-3">
              <div className="flex items-center">
                <input
                  type="checkbox"
                  checked={selectedFiles.includes(file.id)}
                  onChange={() => toggleFileSelection(file.id)}
                  className="mr-2 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                />
                <span className="text-2xl mr-2">{getFileIcon(file.type)}</span>
              </div>
              {file.isEncrypted && (
                <LockClosedIcon className="h-4 w-4 text-green-500" />
              )}
            </div>

            <h3 className="font-medium text-gray-900 dark:text-white mb-2 truncate" title={file.name}>
              {file.name}
            </h3>

            <div className="text-sm text-gray-500 dark:text-gray-400 space-y-1">
              <div>Size: {formatFileSize(file.size)}</div>
              <div>Encrypted: {formatFileSize(file.encryptedSize)}</div>
              <div>Uploaded: {file.uploadedAt.toLocaleDateString()}</div>
            </div>

            <div className="flex justify-between items-center mt-4 pt-3 border-t border-gray-200 dark:border-gray-700">
              <button
                onClick={() => handleDownload(file)}
                className="p-2 text-blue-600 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded-lg transition-colors"
                title="Download"
              >
                <CloudArrowDownIcon className="h-4 w-4" />
              </button>
              <button
                className="p-2 text-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700 rounded-lg transition-colors"
                title="Preview"
              >
                <EyeIcon className="h-4 w-4" />
              </button>
              <button
                onClick={() => handleDelete(file.id)}
                className="p-2 text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-lg transition-colors"
                title="Delete"
              >
                <TrashIcon className="h-4 w-4" />
              </button>
            </div>
          </motion.div>
        ))}
      </div>

      {filteredFiles.length === 0 && (
        <div className="text-center py-12">
          <FolderIcon className="h-12 w-12 text-gray-400 mx-auto mb-4" />
          <h3 className="text-lg font-medium text-gray-900 dark:text-white mb-2">
            No files found
          </h3>
          <p className="text-gray-500 dark:text-gray-400">
            {searchTerm ? 'Try adjusting your search terms' : 'Upload your first file to get started'}
          </p>
        </div>
      )}
    </div>
  )
}