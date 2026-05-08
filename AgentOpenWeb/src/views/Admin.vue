<template>
  <div class="admin-container">
    <div class="admin-sidebar">
      <div class="sidebar-header">
        <h2>Solo旅行管理后台</h2>
      </div>
      <nav class="sidebar-nav">
        <button 
          v-for="item in menuItems" 
          :key="item.key"
          :class="['nav-item', { active: activeTab === item.key }]"
          @click="activeTab = item.key"
        >
          <span class="nav-icon">{{ item.icon }}</span>
          <span>{{ item.label }}</span>
        </button>
      </nav>
    </div>

    <div class="admin-content">
      <div class="content-header">
        <h1>{{ currentTitle }}</h1>
        <div class="header-actions">
          <button class="btn-refresh" @click="refreshData">
            🔄 刷新
          </button>
        </div>
      </div>

      <!-- 设置管理 -->
      <div v-if="activeTab === 'settings'" class="content-body">
        <div class="settings-panel">
          <h3>系统设置</h3>
          <div class="settings-grid">
            <div class="setting-item">
              <label>应用名称</label>
              <input v-model="settings.appName" type="text" class="form-input" />
            </div>
            <div class="setting-item">
              <label>应用版本</label>
              <input v-model="settings.appVersion" type="text" class="form-input" readonly />
            </div>
            <div class="setting-item">
              <label>主机地址</label>
              <input v-model="settings.host" type="text" class="form-input" />
            </div>
            <div class="setting-item">
              <label>端口号</label>
              <input v-model="settings.port" type="number" class="form-input" />
            </div>
            <div class="setting-item">
              <label>日志级别</label>
              <select v-model="settings.logLevel" class="form-input">
                <option value="DEBUG">DEBUG</option>
                <option value="INFO">INFO</option>
                <option value="WARN">WARN</option>
                <option value="ERROR">ERROR</option>
              </select>
            </div>
            <div class="setting-item">
              <label>CORS 允许的源</label>
              <textarea v-model="settings.corsOrigins" class="form-input textarea"></textarea>
            </div>
          </div>

          <h3>API 配置状态</h3>
          <div class="status-grid">
            <div :class="['status-card', settings.hasOpenAiKey ? 'status-healthy' : 'status-warning']">
              <div class="status-icon">{{ settings.hasOpenAiKey ? '✅' : '⚠️' }}</div>
              <div class="status-info">
                <span class="status-label">LLM API Key</span>
                <span class="status-value">{{ settings.hasOpenAiKey ? '已配置' : '未配置' }}</span>
              </div>
            </div>
            <div :class="['status-card', settings.hasAmapKey ? 'status-healthy' : 'status-warning']">
              <div class="status-icon">{{ settings.hasAmapKey ? '✅' : '⚠️' }}</div>
              <div class="status-info">
                <span class="status-label">高德地图 Key</span>
                <span class="status-value">{{ settings.hasAmapKey ? '已配置' : '未配置' }}</span>
              </div>
            </div>
            <div :class="['status-card', settings.hasGoogleMapsKey ? 'status-healthy' : 'status-warning']">
              <div class="status-icon">{{ settings.hasGoogleMapsKey ? '✅' : '⚠️' }}</div>
              <div class="status-info">
                <span class="status-label">Google Maps Key</span>
                <span class="status-value">{{ settings.hasGoogleMapsKey ? '已配置' : '未配置' }}</span>
              </div>
            </div>
            <div :class="['status-card', settings.hasXhsCookie ? 'status-healthy' : 'status-warning']">
              <div class="status-icon">{{ settings.hasXhsCookie ? '✅' : '⚠️' }}</div>
              <div class="status-info">
                <span class="status-label">小红书 Cookie</span>
                <span class="status-value">{{ settings.hasXhsCookie ? '已配置' : '未配置' }}</span>
              </div>
            </div>
          </div>

          <h3>LLM 配置</h3>
          <div class="settings-grid">
            <div class="setting-item">
              <label>API 基础 URL</label>
              <input v-model="settings.openAiBaseUrl" type="text" class="form-input" />
            </div>
            <div class="setting-item">
              <label>模型名称</label>
              <input v-model="settings.openAiModel" type="text" class="form-input" />
            </div>
          </div>

          <div class="form-actions">
            <button class="btn-save" @click="saveSettings">保存设置</button>
            <button class="btn-cancel" @click="resetSettings">重置</button>
          </div>
        </div>
      </div>

      <!-- 健康检查 -->
      <div v-if="activeTab === 'health'" class="content-body">
        <div class="health-panel">
          <div :class="['health-overview', `health-${healthStatus.overallStatus}`]">
            <div class="health-icon">{{ getHealthIcon(healthStatus.overallStatus) }}</div>
            <div class="health-info">
              <span class="health-title">系统状态</span>
              <span class="health-value">{{ getHealthText(healthStatus.overallStatus) }}</span>
            </div>
          </div>

          <div class="health-grid">
            <div 
              v-for="check in healthStatus.checks" 
              :key="check.name"
              :class="['health-card', `health-${check.status}`]"
            >
              <div class="health-card-icon">{{ getCheckIcon(check.status) }}</div>
              <div class="health-card-content">
                <h4>{{ check.name }}</h4>
                <p>{{ check.message }}</p>
              </div>
            </div>
          </div>

          <div class="system-info">
            <h3>系统信息</h3>
            <div class="info-grid">
              <div class="info-item">
                <span class="info-label">运行时间</span>
                <span class="info-value">{{ healthStatus.uptime }}</span>
              </div>
              <div class="info-item">
                <span class="info-label">检查时间</span>
                <span class="info-value">{{ formatTimestamp(healthStatus.timestamp) }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- 旅行计划管理 -->
      <div v-if="activeTab === 'trips'" class="content-body">
        <div class="trips-panel">
          <div class="trips-header">
            <div class="search-box">
              <input type="text" v-model="searchKeyword" placeholder="搜索城市..." class="form-input" />
            </div>
            <div class="filter-box">
              <select v-model="filterStatus" class="form-input">
                <option value="all">全部</option>
                <option value="completed">已完成</option>
                <option value="processing">处理中</option>
                <option value="failed">失败</option>
              </select>
            </div>
          </div>

          <div class="trips-table">
            <table>
              <thead>
                <tr>
                  <th>计划ID</th>
                  <th>城市</th>
                  <th>天数</th>
                  <th>状态</th>
                  <th>创建时间</th>
                  <th>操作</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in filteredTrips" :key="item.plan_id">
                  <td>{{ item.plan_id }}</td>
                  <td>{{ item.city }}</td>
                  <td>{{ item.travel_days }}天</td>
                  <td>
                    <!-- <span :class="['status-badge', `status-${item.status || 'completed'}`]">
                      {{ item.status || 'completed' }}
                    </span> -->
                  </td>
                  <td>{{ formatDate(item.updated_at) }}</td>
                  <td>
                    <button class="btn-view" @click="viewTrip(item.plan_id)">查看</button>
                    <button class="btn-delete" @click="deleteTrip(item.plan_id)">删除</button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div v-if="filteredTrips.length === 0" class="empty-state">
            <div class="empty-icon">📋</div>
            <p>暂无旅行计划记录</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import type { AdminSettings, HealthCheckResponse, TripHistoryItem } from '@/types'
import { getAdminSettings, updateAdminSettings, getAdminHealthDetails, getTripHistory } from '@/services/api'

const activeTab = ref('settings')
const settings = ref<AdminSettings>({
  appName: '',
  appVersion: '',
  debug: false,
  host: '',
  port: 8000,
  corsOrigins: '',
  hasAmapKey: false,
  hasGoogleMapsKey: false,
  hasOpenAiKey: false,
  hasXhsCookie: false,
  openAiBaseUrl: '',
  openAiModel: '',
  logLevel: 'INFO'
})
const originalSettings = ref<AdminSettings>({ ...settings.value })
const healthStatus = ref<HealthCheckResponse>({
  success: false,
  overallStatus: 'degraded',
  checks: [],
  timestamp: '',
  uptime: ''
})
const trips = ref<TripHistoryItem[]>([])
const searchKeyword = ref('')
const filterStatus = ref('all')

const menuItems = [
  { key: 'settings', label: '系统设置', icon: '⚙️' },
  { key: 'health', label: '健康检查', icon: '❤️' },
  { key: 'trips', label: '计划管理', icon: '📋' }
]

const currentTitle = computed(() => {
  const item = menuItems.find(i => i.key === activeTab.value)
  return item?.label || ''
})

const filteredTrips = computed(() => {
  let result = trips.value
  if (searchKeyword.value) {
    const keyword = searchKeyword.value.toLowerCase()
    result = result.filter(t => t.city.toLowerCase().includes(keyword))
  }
  if (filterStatus.value !== 'all') {
    // result = result.filter(t => t.status === filterStatus.value)
  }
  return result
})

const getHealthIcon = (status: string) => {
  return status === 'healthy' ? '🟢' : status === 'degraded' ? '🟡' : '🔴'
}

const getHealthText = (status: string) => {
  return status === 'healthy' ? '运行正常' : status === 'degraded' ? '部分服务异常' : '系统异常'
}

const getCheckIcon = (status: string) => {
  return status === 'healthy' ? '✅' : status === 'warning' ? '⚠️' : '❌'
}

const formatTimestamp = (timestamp: string) => {
  if (!timestamp) return '-'
  try {
    return new Date(timestamp).toLocaleString('zh-CN')
  } catch {
    return timestamp
  }
}

const formatDate = (dateStr: string) => {
  if (!dateStr) return '-'
  try {
    return new Date(dateStr).toLocaleDateString('zh-CN')
  } catch {
    return dateStr
  }
}

const refreshData = () => {
  if (activeTab.value === 'settings') {
    loadSettings()
  } else if (activeTab.value === 'health') {
    loadHealth()
  } else if (activeTab.value === 'trips') {
    loadTrips()
  }
}

const loadSettings = async () => {
  try {
    settings.value = await getAdminSettings()
    originalSettings.value = { ...settings.value }
  } catch (error) {
    console.error('加载设置失败:', error)
  }
}

const loadHealth = async () => {
  try {
    healthStatus.value = await getAdminHealthDetails()
  } catch (error) {
    console.error('加载健康检查失败:', error)
  }
}

const loadTrips = async () => {
  try {
    trips.value = await getTripHistory(20)
  } catch (error) {
    console.error('加载旅行计划失败:', error)
  }
}

const saveSettings = async () => {
  try {
    const updates: Record<string, any> = {}
    if (settings.value.appName !== originalSettings.value.appName) updates.appName = settings.value.appName
    if (settings.value.host !== originalSettings.value.host) updates.host = settings.value.host
    if (settings.value.port !== originalSettings.value.port) updates.port = settings.value.port
    if (settings.value.logLevel !== originalSettings.value.logLevel) updates.logLevel = settings.value.logLevel
    if (settings.value.corsOrigins !== originalSettings.value.corsOrigins) updates.corsOrigins = settings.value.corsOrigins
    if (settings.value.openAiBaseUrl !== originalSettings.value.openAiBaseUrl) updates.openAiBaseUrl = settings.value.openAiBaseUrl
    if (settings.value.openAiModel !== originalSettings.value.openAiModel) updates.openAiModel = settings.value.openAiModel

    if (Object.keys(updates).length > 0) {
      await updateAdminSettings(updates)
      alert('设置已保存！部分设置需要重启服务才能生效。')
      await loadSettings()
    } else {
      alert('没有需要保存的更改。')
    }
  } catch (error) {
    console.error('保存设置失败:', error)
    alert('保存设置失败，请重试。')
  }
}

const resetSettings = () => {
  settings.value = { ...originalSettings.value }
}

const viewTrip = (planId: string) => {
  window.open(`/result/${planId}`, '_blank')
}

const deleteTrip = (planId: string) => {
  if (confirm(`确定要删除计划 ${planId} 吗？`)) {
    trips.value = trips.value.filter(t => t.plan_id !== planId)
    alert('删除成功！')
  }
}

onMounted(() => {
  loadSettings()
  loadHealth()
  loadTrips()
})
</script>

<style scoped>
.admin-container {
  display: flex;
  min-height: 100vh;
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
}

.admin-sidebar {
  width: 240px;
  background: rgba(255, 255, 255, 0.05);
  border-right: 1px solid rgba(255, 255, 255, 0.1);
  padding: 20px 0;
}

.sidebar-header {
  padding: 0 20px 20px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.sidebar-header h2 {
  color: #fff;
  font-size: 18px;
  font-weight: 600;
  margin: 0;
}

.sidebar-nav {
  margin-top: 20px;
}

.nav-item {
  width: 100%;
  padding: 14px 20px;
  border: none;
  background: transparent;
  color: rgba(255, 255, 255, 0.7);
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 10px;
  transition: all 0.3s;
  font-size: 14px;
}

.nav-item:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #fff;
}

.nav-item.active {
  background: rgba(74, 144, 226, 0.3);
  color: #fff;
  border-left: 3px solid #4a90e2;
}

.nav-icon {
  font-size: 16px;
}

.admin-content {
  flex: 1;
  padding: 24px;
  overflow-y: auto;
}

.content-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.content-header h1 {
  color: #fff;
  font-size: 24px;
  margin: 0;
}

.header-actions {
  display: flex;
  gap: 12px;
}

.btn-refresh {
  padding: 10px 20px;
  border: 1px solid rgba(255, 255, 255, 0.2);
  background: rgba(255, 255, 255, 0.1);
  color: #fff;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.3s;
  font-size: 14px;
}

.btn-refresh:hover {
  background: rgba(255, 255, 255, 0.2);
}

.content-body {
  background: rgba(255, 255, 255, 0.05);
  border-radius: 12px;
  padding: 24px;
}

.settings-panel h3 {
  color: #fff;
  font-size: 16px;
  margin: 0 0 20px;
  padding-bottom: 10px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.settings-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.setting-item {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.setting-item label {
  color: rgba(255, 255, 255, 0.8);
  font-size: 14px;
}

.form-input {
  padding: 12px 16px;
  border: 1px solid rgba(255, 255, 255, 0.2);
  background: rgba(255, 255, 255, 0.05);
  color: #fff;
  border-radius: 8px;
  font-size: 14px;
  transition: all 0.3s;
}

.form-input:focus {
  outline: none;
  border-color: #4a90e2;
}

.form-input:disabled,
.form-input[readonly] {
  opacity: 0.5;
  cursor: not-allowed;
}

.form-input.textarea {
  min-height: 80px;
  resize: vertical;
}

.status-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.status-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.05);
}

.status-card.status-healthy {
  border: 1px solid rgba(72, 187, 120, 0.3);
}

.status-card.status-warning {
  border: 1px solid rgba(251, 146, 60, 0.3);
}

.status-icon {
  font-size: 24px;
}

.status-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.status-label {
  color: rgba(255, 255, 255, 0.6);
  font-size: 12px;
}

.status-value {
  color: #fff;
  font-size: 14px;
  font-weight: 500;
}

.form-actions {
  display: flex;
  gap: 12px;
  justify-content: flex-end;
  margin-top: 24px;
}

.btn-save {
  padding: 12px 32px;
  background: linear-gradient(135deg, #4a90e2 0%, #357abd 100%);
  color: #fff;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  font-size: 14px;
  font-weight: 500;
  transition: all 0.3s;
}

.btn-save:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 15px rgba(74, 144, 226, 0.4);
}

.btn-cancel {
  padding: 12px 32px;
  border: 1px solid rgba(255, 255, 255, 0.2);
  background: transparent;
  color: #fff;
  border-radius: 8px;
  cursor: pointer;
  font-size: 14px;
  transition: all 0.3s;
}

.btn-cancel:hover {
  background: rgba(255, 255, 255, 0.1);
}

.health-panel {
  margin-bottom: 24px;
}

.health-overview {
  display: flex;
  align-items: center;
  gap: 20px;
  padding: 24px;
  border-radius: 12px;
  margin-bottom: 24px;
}

.health-overview.health-healthy {
  background: rgba(72, 187, 120, 0.15);
  border: 1px solid rgba(72, 187, 120, 0.3);
}

.health-overview.health-degraded {
  background: rgba(251, 146, 60, 0.15);
  border: 1px solid rgba(251, 146, 60, 0.3);
}

.health-overview.health-critical {
  background: rgba(239, 68, 68, 0.15);
  border: 1px solid rgba(239, 68, 68, 0.3);
}

.health-icon {
  font-size: 48px;
}

.health-info {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.health-title {
  color: rgba(255, 255, 255, 0.7);
  font-size: 14px;
}

.health-value {
  color: #fff;
  font-size: 24px;
  font-weight: 600;
}

.health-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.health-card {
  display: flex;
  gap: 16px;
  padding: 20px;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.05);
}

.health-card.health-healthy {
  border-left: 4px solid #48bb78;
}

.health-card.health-warning {
  border-left: 4px solid #fb923c;
}

.health-card.health-critical {
  border-left: 4px solid #ef4444;
}

.health-card-icon {
  font-size: 28px;
}

.health-card-content h4 {
  color: #fff;
  font-size: 16px;
  margin: 0 0 8px;
}

.health-card-content p {
  color: rgba(255, 255, 255, 0.7);
  font-size: 14px;
  margin: 0;
}

.system-info {
  background: rgba(255, 255, 255, 0.05);
  border-radius: 12px;
  padding: 20px;
}

.system-info h3 {
  color: #fff;
  font-size: 16px;
  margin: 0 0 16px;
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
}

.info-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 8px;
}

.info-label {
  color: rgba(255, 255, 255, 0.7);
  font-size: 14px;
}

.info-value {
  color: #fff;
  font-size: 14px;
  font-family: monospace;
}

.trips-panel {
  margin-bottom: 24px;
}

.trips-header {
  display: flex;
  gap: 16px;
  margin-bottom: 20px;
}

.search-box,
.filter-box {
  flex: 1;
  max-width: 300px;
}

.trips-table {
  overflow-x: auto;
}

.trips-table table {
  width: 100%;
  border-collapse: collapse;
}

.trips-table th,
.trips-table td {
  padding: 12px 16px;
  text-align: left;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.trips-table th {
  color: rgba(255, 255, 255, 0.7);
  font-size: 14px;
  font-weight: 500;
}

.trips-table td {
  color: #fff;
  font-size: 14px;
}

.status-badge {
  padding: 4px 12px;
  border-radius: 20px;
  font-size: 12px;
  font-weight: 500;
}

.status-badge.status-completed {
  background: rgba(72, 187, 120, 0.2);
  color: #48bb78;
}

.status-badge.status-processing {
  background: rgba(59, 130, 246, 0.2);
  color: #3b82f6;
}

.status-badge.status-failed {
  background: rgba(239, 68, 68, 0.2);
  color: #ef4444;
}

.btn-view,
.btn-delete {
  padding: 6px 12px;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 12px;
  transition: all 0.3s;
}

.btn-view {
  background: rgba(74, 144, 226, 0.2);
  color: #4a90e2;
  margin-right: 8px;
}

.btn-view:hover {
  background: rgba(74, 144, 226, 0.3);
}

.btn-delete {
  background: rgba(239, 68, 68, 0.2);
  color: #ef4444;
}

.btn-delete:hover {
  background: rgba(239, 68, 68, 0.3);
}

.empty-state {
  text-align: center;
  padding: 48px;
  color: rgba(255, 255, 255, 0.5);
}

.empty-icon {
  font-size: 48px;
  margin-bottom: 16px;
}

.empty-state p {
  margin: 0;
  font-size: 14px;
}

@media (max-width: 768px) {
  .admin-container {
    flex-direction: column;
  }

  .admin-sidebar {
    width: 100%;
    border-right: none;
    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  }

  .sidebar-nav {
    display: flex;
    flex-wrap: wrap;
  }

  .nav-item {
    flex: 1;
    min-width: 120px;
    justify-content: center;
  }

  .settings-grid,
  .status-grid,
  .health-grid,
  .info-grid {
    grid-template-columns: 1fr;
  }

  .trips-header {
    flex-direction: column;
  }

  .search-box,
  .filter-box {
    max-width: none;
  }
}
</style>
