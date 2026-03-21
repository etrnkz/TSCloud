'use client'

import { useState, useEffect } from 'react'
import { motion } from 'framer-motion'
import {
  ChartBarIcon,
  ClockIcon,
  ShieldCheckIcon,
  ServerIcon,
  ArrowUpIcon,
  ArrowDownIcon,
  ExclamationTriangleIcon,
  CheckCircleIcon
} from '@heroicons/react/24/outline'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar, PieChart, Pie, Cell, AreaChart, Area } from 'recharts'

export default function Analytics() {
  const [timeRange, setTimeRange] = useState('7d')
  const [analytics, setAnalytics] = useState({
    totalFiles: 342,
    totalStorage: 4.7, // GB
    encryptedFiles: 342,
    uploadSpeed: 1.8, // MB/s
    downloadSpeed: 2.4, // MB/s
    securityEvents: 0,
    uptime: 99.9,
    lastBackup: new Date(Date.now() - 3600000) // 1 hour ago
  })

  const performanceData = [
    { time: '00:00', upload: 1.2, download: 2.1, cpu: 15, memory: 45 },
    { time: '04:00', upload: 0.8, download: 1.5, cpu: 12, memory: 42 },
    { time: '08:00', upload: 2.1, download: 3.2, cpu: 25, memory: 55 },
    { time: '12:00', upload: 1.9, download: 2.8, cpu: 22, memory: 52 },
    { time: '16:00', upload: 2.3, download: 3.1, cpu: 28, memory: 58 },
    { time: '20:00', upload: 1.6, download: 2.4, cpu: 18, memory: 48 }
  ]

  const storageData = [
    { name: 'Documents', value: 1.8, color: '#3B82F6' },
    { name: 'Images', value: 1.5, color: '#10B981' },
    { name: 'Videos', value: 0.9, color: '#F59E0B' },
    { name: 'Other', value: 0.5, color: '#8B5CF6' }
  ]

  const activityData = [
    { date: '2024-03-15', uploads: 12, downloads: 8, syncs: 3 },
    { date: '2024-03-16', uploads: 15, downloads: 12, syncs: 5 },
    { date: '2024-03-17', uploads: 8, downloads: 6, syncs: 2 },
    { date: '2024-03-18', uploads: 18, downloads: 14, syncs: 4 },
    { date: '2024-03-19', uploads: 22, downloads: 16, syncs: 6 },
    { date: '2024-03-20', uploads: 14, downloads: 10, syncs: 3 },
    { date: '2024-03-21', uploads: 16, downloads: 12, syncs: 4 }
  ]

  const securityEvents = [
    { time: '2024-03-21 14:30', type: 'Encryption Success', file: 'document.pdf', status: 'success' },
    { time: '2024-03-21 14:25', type: 'File Upload', file: 'presentation.pptx', status: 'success' },
    { time: '2024-03-21 14:20', type: 'Integrity Check', file: 'photo.jpg', status: 'success' },
    { time: '2024-03-21 14:15', type: 'Key Derivation', file: 'spreadsheet.xlsx', status: 'success' },
    { time: '2024-03-21 14:10', type: 'Decryption Success', file: 'video.mp4', status: 'success' }
  ]

  const MetricCard = ({ title, value, unit, icon: Icon, trend, color }: any) => (
    <motion.div
      whileHover={{ scale: 1.02 }}
      className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6"
    >
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-medium text-gray-600 dark:text-gray-400">{title}</p>
          <p className="text-2xl font-bold text-gray-900 dark:text-white">
            {value} {unit}
          </p>
          {trend && (
            <div className="flex items-center mt-1">
              {trend > 0 ? (
                <ArrowUpIcon className="h-4 w-4 text-green-500" />
              ) : (
                <ArrowDownIcon className="h-4 w-4 text-red-500" />
              )}
              <span className={`text-sm ${trend > 0 ? 'text-green-600' : 'text-red-600'}`}>
                {Math.abs(trend)}%
              </span>
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
      {/* Header */}
      <div className="flex justify-between items-center">
        <h2 className="text-2xl font-bold text-gray-900 dark:text-white">
          Analytics & Monitoring
        </h2>
        <select
          value={timeRange}
          onChange={(e) => setTimeRange(e.target.value)}
          className="px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
        >
          <option value="24h">Last 24 Hours</option>
          <option value="7d">Last 7 Days</option>
          <option value="30d">Last 30 Days</option>
          <option value="90d">Last 90 Days</option>
        </select>
      </div>

      {/* Key Metrics */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        <MetricCard
          title="Total Files"
          value={analytics.totalFiles}
          unit=""
          icon={ChartBarIcon}
          trend={12}
          color="bg-blue-500"
        />
        <MetricCard
          title="Storage Used"
          value={analytics.totalStorage}
          unit="GB"
          icon={ServerIcon}
          trend={8}
          color="bg-green-500"
        />
        <MetricCard
          title="Upload Speed"
          value={analytics.uploadSpeed}
          unit="MB/s"
          icon={ArrowUpIcon}
          trend={-3}
          color="bg-purple-500"
        />
        <MetricCard
          title="System Uptime"
          value={analytics.uptime}
          unit="%"
          icon={ShieldCheckIcon}
          trend={0.1}
          color="bg-orange-500"
        />
      </div>

      {/* Charts Row 1 */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Performance Chart */}
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
            Performance Metrics
          </h3>
          <ResponsiveContainer width="100%" height={300}>
            <AreaChart data={performanceData}>
              <CartesianGrid strokeDasharray="3 3" className="opacity-30" />
              <XAxis dataKey="time" />
              <YAxis />
              <Tooltip 
                contentStyle={{
                  backgroundColor: 'rgb(31 41 55)',
                  border: 'none',
                  borderRadius: '8px',
                  color: 'white'
                }}
              />
              <Area 
                type="monotone" 
                dataKey="upload" 
                stackId="1"
                stroke="#3B82F6" 
                fill="#3B82F6"
                fillOpacity={0.6}
              />
              <Area 
                type="monotone" 
                dataKey="download" 
                stackId="1"
                stroke="#10B981" 
                fill="#10B981"
                fillOpacity={0.6}
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>

        {/* Storage Distribution */}
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
            Storage Distribution
          </h3>
          <ResponsiveContainer width="100%" height={300}>
            <PieChart>
              <Pie
                data={storageData}
                cx="50%"
                cy="50%"
                innerRadius={60}
                outerRadius={100}
                paddingAngle={5}
                dataKey="value"
              >
                {storageData.map((entry, index) => (
                  <Cell key={`cell-${index}`} fill={entry.color} />
                ))}
              </Pie>
              <Tooltip 
                contentStyle={{
                  backgroundColor: 'rgb(31 41 55)',
                  border: 'none',
                  borderRadius: '8px',
                  color: 'white'
                }}
                formatter={(value: any) => [`${value} GB`, 'Size']}
              />
            </PieChart>
          </ResponsiveContainer>
          <div className="grid grid-cols-2 gap-4 mt-4">
            {storageData.map((item, index) => (
              <div key={index} className="flex items-center">
                <div 
                  className="w-3 h-3 rounded-full mr-2"
                  style={{ backgroundColor: item.color }}
                />
                <span className="text-sm text-gray-600 dark:text-gray-400">
                  {item.name}: {item.value} GB
                </span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Activity Chart */}
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
          Daily Activity
        </h3>
        <ResponsiveContainer width="100%" height={300}>
          <BarChart data={activityData}>
            <CartesianGrid strokeDasharray="3 3" className="opacity-30" />
            <XAxis dataKey="date" />
            <YAxis />
            <Tooltip 
              contentStyle={{
                backgroundColor: 'rgb(31 41 55)',
                border: 'none',
                borderRadius: '8px',
                color: 'white'
              }}
            />
            <Bar dataKey="uploads" fill="#3B82F6" name="Uploads" />
            <Bar dataKey="downloads" fill="#10B981" name="Downloads" />
            <Bar dataKey="syncs" fill="#F59E0B" name="Syncs" />
          </BarChart>
        </ResponsiveContainer>
      </div>

      {/* Security Events */}
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700">
        <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
            Recent Security Events
          </h3>
        </div>
        <div className="divide-y divide-gray-200 dark:divide-gray-700">
          {securityEvents.map((event, index) => (
            <motion.div
              key={index}
              initial={{ opacity: 0, x: -20 }}
              animate={{ opacity: 1, x: 0 }}
              className="px-6 py-4 hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center">
                  <div className={`p-2 rounded-full ${
                    event.status === 'success' 
                      ? 'bg-green-100 dark:bg-green-900' 
                      : 'bg-red-100 dark:bg-red-900'
                  }`}>
                    {event.status === 'success' ? (
                      <CheckCircleIcon className="h-4 w-4 text-green-600 dark:text-green-400" />
                    ) : (
                      <ExclamationTriangleIcon className="h-4 w-4 text-red-600 dark:text-red-400" />
                    )}
                  </div>
                  <div className="ml-4">
                    <p className="text-sm font-medium text-gray-900 dark:text-white">
                      {event.type}
                    </p>
                    <p className="text-sm text-gray-500 dark:text-gray-400">
                      {event.file} • {event.time}
                    </p>
                  </div>
                </div>
                <span className={`px-2 py-1 text-xs rounded-full ${
                  event.status === 'success'
                    ? 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200'
                    : 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200'
                }`}>
                  {event.status}
                </span>
              </div>
            </motion.div>
          ))}
        </div>
      </div>

      {/* System Health */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6 text-center">
          <ShieldCheckIcon className="h-8 w-8 text-green-500 mx-auto mb-2" />
          <p className="text-2xl font-bold text-gray-900 dark:text-white">Secure</p>
          <p className="text-sm text-gray-600 dark:text-gray-400">All files encrypted</p>
        </div>
        
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6 text-center">
          <ClockIcon className="h-8 w-8 text-blue-500 mx-auto mb-2" />
          <p className="text-2xl font-bold text-gray-900 dark:text-white">
            {analytics.lastBackup.toLocaleTimeString()}
          </p>
          <p className="text-sm text-gray-600 dark:text-gray-400">Last Backup</p>
        </div>
        
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6 text-center">
          <ServerIcon className="h-8 w-8 text-purple-500 mx-auto mb-2" />
          <p className="text-2xl font-bold text-gray-900 dark:text-white">Online</p>
          <p className="text-sm text-gray-600 dark:text-gray-400">System Status</p>
        </div>
      </div>
    </div>
  )
}