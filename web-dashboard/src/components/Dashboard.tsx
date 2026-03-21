'use client'

import { useState, useEffect } from 'react'
import { motion } from 'framer-motion'
import {
  CloudIcon,
  DocumentIcon,
  FolderIcon,
  LockClosedIcon,
  ArrowUpIcon,
  ArrowDownIcon,
  ClockIcon
} from '@heroicons/react/24/outline'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar } from 'recharts'

export default function Dashboard() {
  const [stats, setStats] = useState({
    totalFiles: 127,
    totalStorage: '2.4 GB',
    encryptedFiles: 127,
    syncedFolders: 3,
    lastSync: '2 minutes ago',
    uploadSpeed: '1.2 MB/s',
    downloadSpeed: '2.1 MB/s'
  })

  const [recentActivity, setRecentActivity] = useState([
    { id: 1, type: 'upload', file: 'document.pdf', time: '2 min ago', size: '1.2 MB' },
    { id: 2, type: 'sync', file: 'Photos folder', time: '5 min ago', size: '15.3 MB' },
    { id: 3, type: 'download', file: 'presentation.pptx', time: '8 min ago', size: '3.4 MB' },
    { id: 4, type: 'encrypt', file: 'confidential.docx', time: '12 min ago', size: '0.8 MB' },
    { id: 5, type: 'upload', file: 'image.jpg', time: '15 min ago', size: '2.1 MB' }
  ])

  const storageData = [
    { name: 'Jan', storage: 0.8 },
    { name: 'Feb', storage: 1.2 },
    { name: 'Mar', storage: 1.8 },
    { name: 'Apr', storage: 2.1 },
    { name: 'May', storage: 2.4 }
  ]

  const activityData = [
    { name: 'Mon', uploads: 12, downloads: 8 },
    { name: 'Tue', uploads: 19, downloads: 15 },
    { name: 'Wed', uploads: 8, downloads: 12 },
    { name: 'Thu', uploads: 15, downloads: 9 },
    { name: 'Fri', uploads: 22, downloads: 18 },
    { name: 'Sat', uploads: 6, downloads: 4 },
    { name: 'Sun', uploads: 3, downloads: 2 }
  ]

  const StatCard = ({ title, value, icon: Icon, color, change }: any) => (
    <motion.div
      whileHover={{ scale: 1.02 }}
      className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6"
    >
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-medium text-gray-600 dark:text-gray-400">{title}</p>
          <p className="text-2xl font-bold text-gray-900 dark:text-white">{value}</p>
          {change && (
            <div className="flex items-center mt-1">
              <ArrowUpIcon className="h-4 w-4 text-green-500" />
              <span className="text-sm text-green-600 dark:text-green-400">{change}</span>
            </div>
          )}
        </div>
        <div className={`p-3 rounded-full ${color}`}>
          <Icon className="h-6 w-6 text-white" />
        </div>
      </div>
    </motion.div>
  )

  return (
    <div className="space-y-8">
      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        <StatCard
          title="Total Files"
          value={stats.totalFiles}
          icon={DocumentIcon}
          color="bg-blue-500"
          change="+12 this week"
        />
        <StatCard
          title="Storage Used"
          value={stats.totalStorage}
          icon={CloudIcon}
          color="bg-green-500"
          change="+0.3 GB this week"
        />
        <StatCard
          title="Encrypted Files"
          value={stats.encryptedFiles}
          icon={LockClosedIcon}
          color="bg-purple-500"
          change="100% encrypted"
        />
        <StatCard
          title="Synced Folders"
          value={stats.syncedFolders}
          icon={FolderIcon}
          color="bg-orange-500"
          change="All active"
        />
      </div>

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Storage Usage Chart */}
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
            Storage Usage Over Time
          </h3>
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={storageData}>
              <CartesianGrid strokeDasharray="3 3" className="opacity-30" />
              <XAxis dataKey="name" />
              <YAxis />
              <Tooltip 
                contentStyle={{
                  backgroundColor: 'rgb(31 41 55)',
                  border: 'none',
                  borderRadius: '8px',
                  color: 'white'
                }}
              />
              <Line 
                type="monotone" 
                dataKey="storage" 
                stroke="#3B82F6" 
                strokeWidth={3}
                dot={{ fill: '#3B82F6', strokeWidth: 2, r: 4 }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>

        {/* Activity Chart */}
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
            Weekly Activity
          </h3>
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={activityData}>
              <CartesianGrid strokeDasharray="3 3" className="opacity-30" />
              <XAxis dataKey="name" />
              <YAxis />
              <Tooltip 
                contentStyle={{
                  backgroundColor: 'rgb(31 41 55)',
                  border: 'none',
                  borderRadius: '8px',
                  color: 'white'
                }}
              />
              <Bar dataKey="uploads" fill="#10B981" />
              <Bar dataKey="downloads" fill="#3B82F6" />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* Recent Activity */}
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700">
        <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
            Recent Activity
          </h3>
        </div>
        <div className="divide-y divide-gray-200 dark:divide-gray-700">
          {recentActivity.map((activity) => (
            <motion.div
              key={activity.id}
              initial={{ opacity: 0, x: -20 }}
              animate={{ opacity: 1, x: 0 }}
              className="px-6 py-4 hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center">
                  <div className={`p-2 rounded-full ${
                    activity.type === 'upload' ? 'bg-green-100 dark:bg-green-900' :
                    activity.type === 'download' ? 'bg-blue-100 dark:bg-blue-900' :
                    activity.type === 'sync' ? 'bg-purple-100 dark:bg-purple-900' :
                    'bg-orange-100 dark:bg-orange-900'
                  }`}>
                    {activity.type === 'upload' && <ArrowUpIcon className="h-4 w-4 text-green-600 dark:text-green-400" />}
                    {activity.type === 'download' && <ArrowDownIcon className="h-4 w-4 text-blue-600 dark:text-blue-400" />}
                    {activity.type === 'sync' && <FolderIcon className="h-4 w-4 text-purple-600 dark:text-purple-400" />}
                    {activity.type === 'encrypt' && <LockClosedIcon className="h-4 w-4 text-orange-600 dark:text-orange-400" />}
                  </div>
                  <div className="ml-4">
                    <p className="text-sm font-medium text-gray-900 dark:text-white">
                      {activity.type.charAt(0).toUpperCase() + activity.type.slice(1)}ed {activity.file}
                    </p>
                    <p className="text-sm text-gray-500 dark:text-gray-400">
                      {activity.size} • {activity.time}
                    </p>
                  </div>
                </div>
                <ClockIcon className="h-4 w-4 text-gray-400" />
              </div>
            </motion.div>
          ))}
        </div>
      </div>

      {/* Quick Stats */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6 text-center">
          <ArrowUpIcon className="h-8 w-8 text-green-500 mx-auto mb-2" />
          <p className="text-2xl font-bold text-gray-900 dark:text-white">{stats.uploadSpeed}</p>
          <p className="text-sm text-gray-600 dark:text-gray-400">Average Upload Speed</p>
        </div>
        
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6 text-center">
          <ArrowDownIcon className="h-8 w-8 text-blue-500 mx-auto mb-2" />
          <p className="text-2xl font-bold text-gray-900 dark:text-white">{stats.downloadSpeed}</p>
          <p className="text-sm text-gray-600 dark:text-gray-400">Average Download Speed</p>
        </div>
        
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6 text-center">
          <ClockIcon className="h-8 w-8 text-purple-500 mx-auto mb-2" />
          <p className="text-2xl font-bold text-gray-900 dark:text-white">{stats.lastSync}</p>
          <p className="text-sm text-gray-600 dark:text-gray-400">Last Sync</p>
        </div>
      </div>
    </div>
  )
}