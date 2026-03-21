'use client'

import { useState, useEffect } from 'react'
import { motion } from 'framer-motion'
import {
  FolderIcon,
  PlusIcon,
  TrashIcon,
  PlayIcon,
  PauseIcon,
  Cog6ToothIcon,
  CloudArrowUpIcon,
  ExclamationTriangleIcon,
  CheckCircleIcon
} from '@heroicons/react/24/outline'

interface SyncedFolder {
  id: string
  path: string
  name: string
  isActive: boolean
  lastSync: Date
  fileCount: number
  totalSize: number
  syncedFiles: number
  status: 'idle' | 'syncing' | 'error' | 'paused'
  autoSync: boolean
  includeSubfolders: boolean
}

export default function FolderSync() {
  const [folders, setFolders] = useState<SyncedFolder[]>([])
  const [showAddDialog, setShowAddDialog] = useState(false)
  const [newFolderPath, setNewFolderPath] = useState('')

  useEffect(() => {
    // Load demo folders
    setFolders([
      {
        id: '1',
        path: '/Users/john/Documents',
        name: 'Documents',
        isActive: true,
        lastSync: new Date(Date.now() - 300000), // 5 minutes ago
        fileCount: 127,
        totalSize: 2400000000, // 2.4 GB
        syncedFiles: 127,
        status: 'idle',
        autoSync: true,
        includeSubfolders: true
      },
      {
        id: '2',
        path: '/Users/john/Pictures',
        name: 'Pictures',
        isActive: true,
        lastSync: new Date(Date.now() - 1800000), // 30 minutes ago
        fileCount: 89,
        totalSize: 1500000000, // 1.5 GB
        syncedFiles: 85,
        status: 'syncing',
        autoSync: true,
        includeSubfolders: false
      },
      {
        id: '3',
        path: '/Users/john/Projects',
        name: 'Projects',
        isActive: false,
        lastSync: new Date(Date.now() - 86400000), // 1 day ago
        fileCount: 234,
        totalSize: 800000000, // 800 MB
        syncedFiles: 230,
        status: 'paused',
        autoSync: false,
        includeSubfolders: true
      }
    ])
  }, [])

  const formatFileSize = (bytes: number) => {
    if (bytes === 0) return '0 Bytes'
    const k = 1024
    const sizes = ['Bytes', 'KB', 'MB', 'GB']
    const i = Math.floor(Math.log(bytes) / Math.log(k))
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
  }

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'syncing':
        return <CloudArrowUpIcon className="h-5 w-5 text-blue-500 animate-pulse" />
      case 'error':
        return <ExclamationTriangleIcon className="h-5 w-5 text-red-500" />
      case 'paused':
        return <PauseIcon className="h-5 w-5 text-yellow-500" />
      default:
        return <CheckCircleIcon className="h-5 w-5 text-green-500" />
    }
  }

  const getStatusText = (status: string) => {
    switch (status) {
      case 'syncing':
        return 'Syncing...'
      case 'error':
        return 'Error'
      case 'paused':
        return 'Paused'
      default:
        return 'Up to date'
    }
  }

  const handleAddFolder = () => {
    if (!newFolderPath.trim()) return

    const newFolder: SyncedFolder = {
      id: Date.now().toString(),
      path: newFolderPath,
      name: newFolderPath.split('/').pop() || 'Unknown',
      isActive: true,
      lastSync: new Date(),
      fileCount: 0,
      totalSize: 0,
      syncedFiles: 0,
      status: 'idle',
      autoSync: true,
      includeSubfolders: true
    }

    setFolders(prev => [...prev, newFolder])
    setNewFolderPath('')
    setShowAddDialog(false)
  }

  const toggleFolderSync = (folderId: string) => {
    setFolders(prev => prev.map(folder =>
      folder.id === folderId
        ? { ...folder, isActive: !folder.isActive, status: folder.isActive ? 'paused' : 'idle' }
        : folder
    ))
  }

  const removeFolder = (folderId: string) => {
    if (confirm('Are you sure you want to remove this folder from sync?')) {
      setFolders(prev => prev.filter(f => f.id !== folderId))
    }
  }

  const startSync = (folderId: string) => {
    setFolders(prev => prev.map(folder =>
      folder.id === folderId
        ? { ...folder, status: 'syncing', lastSync: new Date() }
        : folder
    ))

    // Simulate sync completion
    setTimeout(() => {
      setFolders(prev => prev.map(folder =>
        folder.id === folderId
          ? { ...folder, status: 'idle' }
          : folder
      ))
    }, 3000)
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex justify-between items-center">
        <h2 className="text-2xl font-bold text-gray-900 dark:text-white">
          Folder Sync
        </h2>
        <button
          onClick={() => setShowAddDialog(true)}
          className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors flex items-center"
        >
          <PlusIcon className="h-4 w-4 mr-2" />
          Add Folder
        </button>
      </div>

      {/* Add Folder Dialog */}
      {showAddDialog && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-800 rounded-lg p-6 w-full max-w-md">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
              Add Folder to Sync
            </h3>
            <input
              type="text"
              placeholder="Enter folder path..."
              value={newFolderPath}
              onChange={(e) => setNewFolderPath(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent mb-4"
              autoFocus
            />
            <div className="flex justify-end space-x-3">
              <button
                onClick={() => setShowAddDialog(false)}
                className="px-4 py-2 text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={handleAddFolder}
                className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
              >
                Add Folder
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Folders List */}
      <div className="space-y-4">
        {folders.map((folder) => (
          <motion.div
            key={folder.id}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6"
          >
            <div className="flex items-start justify-between">
              <div className="flex items-start space-x-4 flex-1">
                <FolderIcon className="h-8 w-8 text-blue-500 mt-1" />
                
                <div className="flex-1">
                  <div className="flex items-center space-x-3 mb-2">
                    <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
                      {folder.name}
                    </h3>
                    {getStatusIcon(folder.status)}
                    <span className="text-sm text-gray-500 dark:text-gray-400">
                      {getStatusText(folder.status)}
                    </span>
                  </div>
                  
                  <p className="text-sm text-gray-600 dark:text-gray-400 mb-3">
                    {folder.path}
                  </p>
                  
                  <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
                    <div>
                      <span className="text-gray-500 dark:text-gray-400">Files:</span>
                      <span className="ml-1 font-medium text-gray-900 dark:text-white">
                        {folder.syncedFiles}/{folder.fileCount}
                      </span>
                    </div>
                    <div>
                      <span className="text-gray-500 dark:text-gray-400">Size:</span>
                      <span className="ml-1 font-medium text-gray-900 dark:text-white">
                        {formatFileSize(folder.totalSize)}
                      </span>
                    </div>
                    <div>
                      <span className="text-gray-500 dark:text-gray-400">Last Sync:</span>
                      <span className="ml-1 font-medium text-gray-900 dark:text-white">
                        {folder.lastSync.toLocaleTimeString()}
                      </span>
                    </div>
                    <div>
                      <span className="text-gray-500 dark:text-gray-400">Auto Sync:</span>
                      <span className="ml-1 font-medium text-gray-900 dark:text-white">
                        {folder.autoSync ? 'On' : 'Off'}
                      </span>
                    </div>
                  </div>
                  
                  {folder.status === 'syncing' && (
                    <div className="mt-3">
                      <div className="flex items-center justify-between mb-1">
                        <span className="text-sm text-gray-600 dark:text-gray-400">
                          Syncing files...
                        </span>
                        <span className="text-sm text-gray-500 dark:text-gray-400">
                          {Math.round((folder.syncedFiles / folder.fileCount) * 100)}%
                        </span>
                      </div>
                      <div className="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-2">
                        <div
                          className="bg-blue-600 h-2 rounded-full transition-all duration-300"
                          style={{ width: `${(folder.syncedFiles / folder.fileCount) * 100}%` }}
                        />
                      </div>
                    </div>
                  )}
                </div>
              </div>
              
              <div className="flex items-center space-x-2 ml-4">
                <button
                  onClick={() => startSync(folder.id)}
                  disabled={folder.status === 'syncing'}
                  className="p-2 text-blue-600 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                  title="Start Sync"
                >
                  <PlayIcon className="h-4 w-4" />
                </button>
                
                <button
                  onClick={() => toggleFolderSync(folder.id)}
                  className={`p-2 rounded-lg transition-colors ${
                    folder.isActive
                      ? 'text-yellow-600 hover:bg-yellow-50 dark:hover:bg-yellow-900/20'
                      : 'text-green-600 hover:bg-green-50 dark:hover:bg-green-900/20'
                  }`}
                  title={folder.isActive ? 'Pause Sync' : 'Resume Sync'}
                >
                  {folder.isActive ? (
                    <PauseIcon className="h-4 w-4" />
                  ) : (
                    <PlayIcon className="h-4 w-4" />
                  )}
                </button>
                
                <button
                  className="p-2 text-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700 rounded-lg transition-colors"
                  title="Settings"
                >
                  <Cog6ToothIcon className="h-4 w-4" />
                </button>
                
                <button
                  onClick={() => removeFolder(folder.id)}
                  className="p-2 text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-lg transition-colors"
                  title="Remove Folder"
                >
                  <TrashIcon className="h-4 w-4" />
                </button>
              </div>
            </div>
          </motion.div>
        ))}
      </div>

      {folders.length === 0 && (
        <div className="text-center py-12">
          <FolderIcon className="h-12 w-12 text-gray-400 mx-auto mb-4" />
          <h3 className="text-lg font-medium text-gray-900 dark:text-white mb-2">
            No folders configured
          </h3>
          <p className="text-gray-500 dark:text-gray-400 mb-4">
            Add folders to automatically sync your files to the cloud
          </p>
          <button
            onClick={() => setShowAddDialog(true)}
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
          >
            Add Your First Folder
          </button>
        </div>
      )}
    </div>
  )
}