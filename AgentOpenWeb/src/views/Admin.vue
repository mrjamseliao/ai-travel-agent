<template>
  <div class="admin-container">
    <div class="admin-sidebar">
      <div class="sidebar-header">
        <h2>边牧旅行管理后台</h2>
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
          <button class="btn-refresh" @click="refreshData">🔄 刷新</button>
          <button class="btn-refresh" @click="goToHome">🏠 返回首页</button>
        </div>
      </div>
      <div class="admin-body">
        <component :is="currentComponent" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import SettingsPanel from './system/SettingsPanel.vue'
import HealthPanel from './system/HealthPanel.vue'
import PlansPanel from './system/PlansPanel.vue'
import AttractionManager from './attraction/AttractionManager.vue'
import HotelManager from './hotel/HotelManager.vue'
import TransportationManager from './transportation/TransportationManager.vue'
import FoodManager from './food/FoodManager.vue'
import RouteManager from './route/RouteManager.vue'
import KnowledgeBaseManager from './knowledge/KnowledgeBaseManager.vue'
import KnowledgeBaseQA from './knowledge/KnowledgeBaseQA.vue'
import RegionManager from './region/RegionManager.vue'

const router = useRouter()

const menuItems = [
  { key: 'settings', label: '系统设置', icon: '⚙️', component: SettingsPanel },
  { key: 'health', label: '健康检查', icon: '❤️', component: HealthPanel },
  { key: 'plans', label: '计划管理', icon: '📋', component: PlansPanel },
  { key: 'attractions', label: '景区管理', icon: '🏞️', component: AttractionManager },
  { key: 'hotels', label: '酒店管理', icon: '🏨', component: HotelManager },
  { key: 'transportation', label: '交通方式', icon: '🚗', component: TransportationManager },
  { key: 'foods', label: '美食管理', icon: '🍜', component: FoodManager },
  { key: 'routes', label: '旅游线路', icon: '🗺️', component: RouteManager },
  { key: 'regions', label: '行政区域', icon: '📍', component: RegionManager },
  { key: 'knowledge', label: '知识库管理', icon: '📚', component: KnowledgeBaseManager },
  { key: 'knowledgeQA', label: '智能问答', icon: '🤖', component: KnowledgeBaseQA }
]

const activeTab = ref('settings')

const currentTitle = computed(() => {
  const item = menuItems.find(i => i.key === activeTab.value)
  return item?.label || ''
})

const currentComponent = computed(() => {
  const item = menuItems.find(i => i.key === activeTab.value)
  return item?.component || SettingsPanel
})

const refreshData = () => {}

const goToHome = () => {
  router.push('/')
}
</script>

<style scoped>
.admin-container { display: flex; height: 100vh; background: #0b1220; color: #e5e7eb; overflow: hidden; }
.admin-sidebar { width: 220px; flex-shrink: 0; background: #0f172a; border-right: 1px solid #1e293b; overflow-y: auto; }
.sidebar-header { padding: 18px 20px; border-bottom: 1px solid #1e293b; }
.sidebar-header h2 { margin: 0; font-size: 15px; color: #fff; font-weight: 700; letter-spacing: 1px; }
.sidebar-nav { display: flex; flex-direction: column; padding: 8px; }
.nav-item { display: flex; align-items: center; gap: 10px; padding: 10px 14px; background: transparent; border: none; color: #94a3b8; cursor: pointer; border-radius: 6px; font-size: 13px; text-align: left; margin-bottom: 2px; transition: all .15s; }
.nav-item:hover { background: #1e293b; color: #e5e7eb; }
.nav-item.active { background: #3b82f6; color: #fff; }
.nav-icon { font-size: 16px; }
.admin-content { flex: 1; display: flex; flex-direction: column; min-width: 0; overflow: hidden; }
.content-header { flex-shrink: 0; display: flex; justify-content: space-between; align-items: center; padding: 14px 24px; background: #0f172a; border-bottom: 1px solid #1e293b; }
.content-header h1 { margin: 0; font-size: 18px; color: #fff; font-weight: 600; }
.header-actions { display: flex; gap: 10px; }
.btn-refresh { background: #3b82f6; color: #fff; border: none; padding: 6px 14px; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-refresh:hover { background: #2563eb; }
.admin-body { flex: 1; min-height: 0; overflow-y: auto; }
</style>
