'use client'

import { useState, useEffect } from 'react'
import { motion } from 'framer-motion'
import {
  Cog6ToothIcon,
  ShieldCheckIcon,
  KeyIcon,
  BellIcon,
  CloudIcon,
  UserIcon,
  DevicePhoneMobileIcon,
  ComputerDesktopIcon,
  GlobeAltIcon
} from '@heroicons/react/24/outline'

export default function Settings() {
  const [settings, setSettings] = useState({
    // Security Settings
    autoEncryption: true,
    twoFactorAuth: false,
    keyRotationDays: 30,
    sessionTimeout: 60,
    
    // Sync Settings
    autoSync: true,
    syncInterval: 5,
    compressFiles: true,
    syncOnlyWifi: false,
    
    // Notification Settings
    uploadNotifications: true,
    downloadNotifications: false,
    errorNotifications: true,
    weeklyReports: true,
    
    // Storage Settings
    maxFileSize: 100, // MB
    storageQuota: 5, // GB
    autoCleanup: true,
    keepVersions: 10,
    
    // Account Settings
    username: 'john.doe',
    email: 'john.doe@example.com',
    timezone: 'UTC-8',
    language: 'en'
  })

  const [activeTab, setActiveTab] = useState('security')
  const [showPasswordDialog, setShowPasswordDialog] = useState(false)
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')

  const tabs = [
    { id: 'security', name: 'Security', icon: ShieldCheckIcon },
    { id: 'sync', name: 'Sync', icon: CloudIcon },
    { id: 'notifications', name: 'Notifications', icon: BellIcon },
    { id: 'storage', name: 'Storage', icon: Cog6ToothIcon },
    { id: 'account', name: 'Account', icon: UserIcon }
  ]

  const updateSetting = (key: string, value: any) => {
    setSettings(prev => ({ ...prev, [key]: value }))
  }

  const handlePasswordChange = () => {
    if (newPassword !== confirmPassword) {
      alert('Passwords do not match')
      return
    }
    if (newPassword.length < 8) {
      alert('Password must be at least 8 characters')
      return
    }
    
    // TODO: Implement password change
    setShowPasswordDialog(false)
    setNewPassword('')
    setConfirmPassword('')
    alert('Password changed successfully')
  }

  const ToggleSwitch = ({ enabled, onChange, label }: any) => (
    <div className="flex items-center justify-between">
      <span className="text-sm font-medium text-gray-700 dark:text-gray-300">{label}</span>
      <button
        onClick={() => onChange(!enabled)}
        className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
          enabled ? 'bg-blue-600' : 'bg-gray-200 dark:bg-gray-700'
        }`}
      >
        <span
          className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
            enabled ? 'translate-x-6' : 'translate-x-1'
          }`}
        />
      </button>
    </div>
  )

  const renderSecuritySettings = () => (
    <div className="space-y-6">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
          Encryption & Security
        </h3>
        <div className="space-y-4">
          <ToggleSwitch
            enabled={settings.autoEncryption}
            onChange={(value: boolean) => updateSetting('autoEncryption', value)}
            label="Automatic file encryption"
          />
          <ToggleSwitch
            enabled={settings.twoFactorAuth}
            onChange={(value: boolean) => updateSetting('twoFactorAuth', value)}
            label="Two-factor authentication"
          />
          
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Key rotation (days)
            </span>
            <select
              value={settings.keyRotationDays}
              onChange={(e) => updateSetting('keyRotationDays', parseInt(e.target.value))}
              className="px-3 py-1 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm"
            >
              <option value={7}>7 days</option>
              <option value={30}>30 days</option>
              <option value={90}>90 days</option>
              <option value={365}>1 year</option>
            </select>
          </div>
          
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Session timeout (minutes)
            </span>
            <select
              value={settings.sessionTimeout}
              onChange={(e) => updateSetting('sessionTimeout', parseInt(e.target.value))}
              className="px-3 py-1 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm"
            >
              <option value={15}>15 minutes</option>
              <option value={30}>30 minutes</option>
              <option value={60}>1 hour</option>
              <option value={240}>4 hours</option>
            </select>
          </div>
        </div>
      </div>

      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
          Password & Authentication
        </h3>
        <button
          onClick={() => setShowPasswordDialog(true)}
          className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors flex items-center"
        >
          <KeyIcon className="h-4 w-4 mr-2" />
          Change Password
        </button>
      </div>
    </div>
  )

  const renderSyncSettings = () => (
    <div className="space-y-6">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
          Synchronization
        </h3>
        <div className="space-y-4">
          <ToggleSwitch
            enabled={settings.autoSync}
            onChange={(value: boolean) => updateSetting('autoSync', value)}
            label="Automatic synchronization"
          />
          <ToggleSwitch
            enabled={settings.compressFiles}
            onChange={(value: boolean) => updateSetting('compressFiles', value)}
            label="Compress files before upload"
          />
          <ToggleSwitch
            enabled={settings.syncOnlyWifi}
            onChange={(value: boolean) => updateSetting('syncOnlyWifi', value)}
            label="Sync only on Wi-Fi"
          />
          
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Sync interval (minutes)
            </span>
            <select
              value={settings.syncInterval}
              onChange={(e) => updateSetting('syncInterval', parseInt(e.target.value))}
              className="px-3 py-1 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm"
            >
              <option value={1}>1 minute</option>
              <option value={5}>5 minutes</option>
              <option value={15}>15 minutes</option>
              <option value={60}>1 hour</option>
            </select>
          </div>
        </div>
      </div>
    </div>
  )

  const renderNotificationSettings = () => (
    <div className="space-y-6">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
          Notification Preferences
        </h3>
        <div className="space-y-4">
          <ToggleSwitch
            enabled={settings.uploadNotifications}
            onChange={(value: boolean) => updateSetting('uploadNotifications', value)}
            label="Upload completion notifications"
          />
          <ToggleSwitch
            enabled={settings.downloadNotifications}
            onChange={(value: boolean) => updateSetting('downloadNotifications', value)}
            label="Download completion notifications"
          />
          <ToggleSwitch
            enabled={settings.errorNotifications}
            onChange={(value: boolean) => updateSetting('errorNotifications', value)}
            label="Error and warning notifications"
          />
          <ToggleSwitch
            enabled={settings.weeklyReports}
            onChange={(value: boolean) => updateSetting('weeklyReports', value)}
            label="Weekly usage reports"
          />
        </div>
      </div>
    </div>
  )

  const renderStorageSettings = () => (
    <div className="space-y-6">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
          Storage Management
        </h3>
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Maximum file size (MB)
            </span>
            <input
              type="number"
              value={settings.maxFileSize}
              onChange={(e) => updateSetting('maxFileSize', parseInt(e.target.value))}
              className="px-3 py-1 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm w-20"
              min="1"
              max="1000"
            />
          </div>
          
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Storage quota (GB)
            </span>
            <input
              type="number"
              value={settings.storageQuota}
              onChange={(e) => updateSetting('storageQuota', parseInt(e.target.value))}
              className="px-3 py-1 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm w-20"
              min="1"
              max="100"
            />
          </div>
          
          <ToggleSwitch
            enabled={settings.autoCleanup}
            onChange={(value: boolean) => updateSetting('autoCleanup', value)}
            label="Automatic cleanup of old files"
          />
          
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
              Keep file versions
            </span>
            <select
              value={settings.keepVersions}
              onChange={(e) => updateSetting('keepVersions', parseInt(e.target.value))}
              className="px-3 py-1 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm"
            >
              <option value={5}>5 versions</option>
              <option value={10}>10 versions</option>
              <option value={20}>20 versions</option>
              <option value={-1}>Unlimited</option>
            </select>
          </div>
        </div>
      </div>
    </div>
  )

  const renderAccountSettings = () => (
    <div className="space-y-6">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
          Account Information
        </h3>
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Username
            </label>
            <input
              type="text"
              value={settings.username}
              onChange={(e) => updateSetting('username', e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
          </div>
          
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Email
            </label>
            <input
              type="email"
              value={settings.email}
              onChange={(e) => updateSetting('email', e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
          </div>
          
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Timezone
            </label>
            <select
              value={settings.timezone}
              onChange={(e) => updateSetting('timezone', e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            >
              <option value="UTC-12">UTC-12</option>
              <option value="UTC-8">UTC-8 (PST)</option>
              <option value="UTC-5">UTC-5 (EST)</option>
              <option value="UTC+0">UTC+0 (GMT)</option>
              <option value="UTC+1">UTC+1 (CET)</option>
              <option value="UTC+8">UTC+8 (CST)</option>
            </select>
          </div>
          
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Language
            </label>
            <select
              value={settings.language}
              onChange={(e) => updateSetting('language', e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            >
              <option value="en">English</option>
              <option value="es">Español</option>
              <option value="fr">Français</option>
              <option value="de">Deutsch</option>
              <option value="zh">中文</option>
            </select>
          </div>
        </div>
      </div>

      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
          Connected Devices
        </h3>
        <div className="space-y-3">
          <div className="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-700 rounded-lg">
            <div className="flex items-center">
              <ComputerDesktopIcon className="h-5 w-5 text-gray-500 mr-3" />
              <div>
                <p className="text-sm font-medium text-gray-900 dark:text-white">Desktop App</p>
                <p className="text-xs text-gray-500 dark:text-gray-400">Last active: 2 minutes ago</p>
              </div>
            </div>
            <span className="px-2 py-1 text-xs bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200 rounded-full">
              Active
            </span>
          </div>
          
          <div className="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-700 rounded-lg">
            <div className="flex items-center">
              <DevicePhoneMobileIcon className="h-5 w-5 text-gray-500 mr-3" />
              <div>
                <p className="text-sm font-medium text-gray-900 dark:text-white">Android App</p>
                <p className="text-xs text-gray-500 dark:text-gray-400">Last active: 1 hour ago</p>
              </div>
            </div>
            <span className="px-2 py-1 text-xs bg-gray-100 text-gray-800 dark:bg-gray-600 dark:text-gray-200 rounded-full">
              Offline
            </span>
          </div>
          
          <div className="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-700 rounded-lg">
            <div className="flex items-center">
              <GlobeAltIcon className="h-5 w-5 text-gray-500 mr-3" />
              <div>
                <p className="text-sm font-medium text-gray-900 dark:text-white">Web Dashboard</p>
                <p className="text-xs text-gray-500 dark:text-gray-400">Current session</p>
              </div>
            </div>
            <span className="px-2 py-1 text-xs bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200 rounded-full">
              Current
            </span>
          </div>
        </div>
      </div>
    </div>
  )

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex justify-between items-center">
        <h2 className="text-2xl font-bold text-gray-900 dark:text-white">
          Settings
        </h2>
        <button className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors">
          Save Changes
        </button>
      </div>

      {/* Navigation Tabs */}
      <div className="border-b border-gray-200 dark:border-gray-700">
        <nav className="flex space-x-8" aria-label="Tabs">
          {tabs.map((tab) => {
            const Icon = tab.icon
            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`${
                  activeTab === tab.id
                    ? 'border-blue-500 text-blue-600 dark:text-blue-400'
                    : 'border-transparent text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300'
                } whitespace-nowrap py-2 px-1 border-b-2 font-medium text-sm flex items-center`}
              >
                <Icon className="h-5 w-5 mr-2" />
                {tab.name}
              </button>
            )
          })}
        </nav>
      </div>

      {/* Tab Content */}
      <motion.div
        key={activeTab}
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.3 }}
      >
        {activeTab === 'security' && renderSecuritySettings()}
        {activeTab === 'sync' && renderSyncSettings()}
        {activeTab === 'notifications' && renderNotificationSettings()}
        {activeTab === 'storage' && renderStorageSettings()}
        {activeTab === 'account' && renderAccountSettings()}
      </motion.div>

      {/* Password Change Dialog */}
      {showPasswordDialog && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-800 rounded-lg p-6 w-full max-w-md">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
              Change Password
            </h3>
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                  New Password
                </label>
                <input
                  type="password"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  placeholder="Enter new password"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                  Confirm Password
                </label>
                <input
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  placeholder="Confirm new password"
                />
              </div>
            </div>
            <div className="flex justify-end space-x-3 mt-6">
              <button
                onClick={() => setShowPasswordDialog(false)}
                className="px-4 py-2 text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={handlePasswordChange}
                className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
              >
                Change Password
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}