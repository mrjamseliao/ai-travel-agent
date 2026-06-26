import axios from 'axios'
import type {
  AdminSettings,
  BackendRuntimeSettings,
  HealthCheckResponse,
  KnowledgeBaseItem,
  KnowledgeBaseRequest,
  KnowledgeBaseSearchRequest,
  KnowledgeBaseQARequest,
  KnowledgeBaseQAResponse,
  RuntimeSettings,
  TripFormData,
  TripHistoryItem,
  TripPlanResponse,
  TripTaskEvent,
} from '@/types'
import { i18n } from '@/i18n'

const ENV_API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''
const ENV_AMAP_WEB_JS_KEY = import.meta.env.VITE_AMAP_WEB_JS_KEY ?? ''
const RUNTIME_API_BASE_STORAGE_KEY = 'SheepTrip.runtime.api_base_url'
const RUNTIME_AMAP_WEB_JS_KEY_STORAGE_KEY = 'SheepTrip.runtime.amap_web_js_key'
const RUNTIME_GOOGLE_MAPS_API_KEY_STORAGE_KEY = 'SheepTrip.runtime.google_maps_api_key'
const DEFAULT_RUNTIME_BACKEND_SETTINGS: BackendRuntimeSettings = {
  vite_amap_web_key: '0bffdc38033b44959a9af8957bda9f47',
  vite_amap_web_js_key: 'ebe35234350890376a4faa7f595ee7e6',
  google_maps_api_key: '',
  google_maps_proxy: '',
  xhs_cookie: '',
  openai_api_key: '',
  openai_base_url: 'https://api.openai.com/v1',
  openai_model: 'gpt-4',
}

export const RUNTIME_SETTINGS_UPDATED_EVENT = 'SheepTrip:runtime-settings-updated'
const t = i18n.global.t

const normalizeBaseUrl = (value: string | null | undefined): string => {
  const text = String(value ?? '').trim()
  return text.replace(/\/+$/, '')
}

const normalizeText = (value: unknown): string => String(value ?? '').trim()

const DEFAULT_API_BASE_URL = normalizeBaseUrl(ENV_API_BASE_URL) || 'http://localhost:8000'
const DEFAULT_AMAP_WEB_JS_KEY = normalizeText(ENV_AMAP_WEB_JS_KEY)

interface SubmitTripPlanResponse {
  task_id: string
  plan_id: string
  status: 'processing'
  ws_url: string
  message: string
}

interface GenerateTripPlanOptions {
  onTaskCreated?: (task: SubmitTripPlanResponse) => void
  onTaskEvent?: (event: TripTaskEvent) => void
}

interface RuntimeSettingsApiResponse {
  success: boolean
  message?: string
  data?: Partial<BackendRuntimeSettings>
}

interface TripHistoryResponse {
  items?: TripHistoryItem[]
}

export const getRuntimeApiBaseUrl = (): string => {
  if (typeof window === 'undefined') {
    return DEFAULT_API_BASE_URL
  }
  const saved = normalizeBaseUrl(window.localStorage.getItem(RUNTIME_API_BASE_STORAGE_KEY))
  return saved || DEFAULT_API_BASE_URL
}

export const setRuntimeApiBaseUrl = (value: string): string => {
  const normalized = normalizeBaseUrl(value) || DEFAULT_API_BASE_URL
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(RUNTIME_API_BASE_STORAGE_KEY, normalized)
  }
  return normalized
}

export const getRuntimeMapJsKey = (): string => {
  if (typeof window === 'undefined') {
    return DEFAULT_AMAP_WEB_JS_KEY || DEFAULT_RUNTIME_BACKEND_SETTINGS.vite_amap_web_js_key
  }
  const saved = normalizeText(window.localStorage.getItem(RUNTIME_AMAP_WEB_JS_KEY_STORAGE_KEY))
  return saved || DEFAULT_AMAP_WEB_JS_KEY || DEFAULT_RUNTIME_BACKEND_SETTINGS.vite_amap_web_js_key
}

export const setRuntimeMapJsKey = (value: string): string => {
  const normalized = normalizeText(value)
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(RUNTIME_AMAP_WEB_JS_KEY_STORAGE_KEY, normalized)
  }
  return normalized
}

export const getRuntimeGoogleMapsApiKey = (): string => {
  if (typeof window === 'undefined') return ''
  return normalizeText(window.localStorage.getItem(RUNTIME_GOOGLE_MAPS_API_KEY_STORAGE_KEY))
}

export const setRuntimeGoogleMapsApiKey = (value: string): string => {
  const normalized = normalizeText(value)
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(RUNTIME_GOOGLE_MAPS_API_KEY_STORAGE_KEY, normalized)
  }
  return normalized
}

const normalizeBackendRuntimeSettings = (
  data?: Partial<BackendRuntimeSettings>
): BackendRuntimeSettings => ({
  vite_amap_web_key: normalizeText(data?.vite_amap_web_key ?? DEFAULT_RUNTIME_BACKEND_SETTINGS.vite_amap_web_key),
  vite_amap_web_js_key: normalizeText(
    data?.vite_amap_web_js_key ?? DEFAULT_RUNTIME_BACKEND_SETTINGS.vite_amap_web_js_key
  ),
  google_maps_api_key: normalizeText(
    data?.google_maps_api_key ?? DEFAULT_RUNTIME_BACKEND_SETTINGS.google_maps_api_key
  ),
  google_maps_proxy: normalizeText(
    data?.google_maps_proxy ?? DEFAULT_RUNTIME_BACKEND_SETTINGS.google_maps_proxy
  ),
  xhs_cookie: normalizeText(data?.xhs_cookie ?? DEFAULT_RUNTIME_BACKEND_SETTINGS.xhs_cookie),
  openai_api_key: normalizeText(data?.openai_api_key ?? DEFAULT_RUNTIME_BACKEND_SETTINGS.openai_api_key),
  openai_base_url:
    normalizeText(data?.openai_base_url ?? DEFAULT_RUNTIME_BACKEND_SETTINGS.openai_base_url) ||
    DEFAULT_RUNTIME_BACKEND_SETTINGS.openai_base_url,
  openai_model:
    normalizeText(data?.openai_model ?? DEFAULT_RUNTIME_BACKEND_SETTINGS.openai_model) ||
    DEFAULT_RUNTIME_BACKEND_SETTINGS.openai_model,
})

const emitRuntimeSettingsUpdated = () => {
  if (typeof window === 'undefined') return
  window.dispatchEvent(new CustomEvent(RUNTIME_SETTINGS_UPDATED_EVENT))
}

const apiClient = axios.create({
  timeout: 0,
  headers: {
    'Content-Type': 'application/json;charset=utf-8'
  }
})

// 请求拦截器
apiClient.interceptors.request.use(
  (config) => {
    config.baseURL = getRuntimeApiBaseUrl()
    console.log('发送请求:', config.method?.toUpperCase(), config.url)
    return config
  },
  (error) => {
    console.error('请求错误:', error)
    return Promise.reject(error)
  }
)

// 响应拦截器
apiClient.interceptors.response.use(
  (response) => {
    console.log('收到响应:', response.status, response.config.url)
    return response
  },
  (error) => {
    console.error('响应错误:', error.response?.status, error.message)
    return Promise.reject(error)
  }
)

export async function getBackendRuntimeSettings(): Promise<BackendRuntimeSettings> {
  try {
    const response = await apiClient.get<RuntimeSettingsApiResponse>('/api/admin/settings')
    return normalizeBackendRuntimeSettings(response.data?.data)
  } catch (error: any) {
    console.error('读取运行时配置失败:', error)
    throw new Error(error.response?.data?.detail || error.message || '读取配置失败')
  }
}

export async function updateBackendRuntimeSettings(
  updates: Partial<BackendRuntimeSettings>
): Promise<BackendRuntimeSettings> {
  try {
    const response = await apiClient.put<RuntimeSettingsApiResponse>('/api/admin/settings', updates)
    return normalizeBackendRuntimeSettings(response.data?.data)
  } catch (error: any) {
    console.error('保存运行时配置失败:', error)
    throw new Error(error.response?.data?.detail || error.message || '保存配置失败')
  }
}

export async function getRuntimeSettings(): Promise<RuntimeSettings> {
  const backend = await getBackendRuntimeSettings()
  const apiBaseUrl = getRuntimeApiBaseUrl()
  const mapJsKey = getRuntimeMapJsKey() || backend.vite_amap_web_js_key

  // 同步 Google Maps API Key 到 localStorage 供前端地图组件读取
  if (backend.google_maps_api_key) {
    setRuntimeGoogleMapsApiKey(backend.google_maps_api_key)
  }

  return {
    api_base_url: apiBaseUrl,
    ...backend,
    vite_amap_web_js_key: mapJsKey,
  }
}

export async function saveRuntimeSettings(settings: RuntimeSettings): Promise<RuntimeSettings> {
  const previousApiBaseUrl = getRuntimeApiBaseUrl()
  const targetApiBaseUrl = normalizeBaseUrl(settings.api_base_url) || previousApiBaseUrl
  const updates: Partial<BackendRuntimeSettings> = {
    vite_amap_web_key: settings.vite_amap_web_key,
    vite_amap_web_js_key: settings.vite_amap_web_js_key,
    google_maps_api_key: settings.google_maps_api_key,
    google_maps_proxy: settings.google_maps_proxy,
    xhs_cookie: settings.xhs_cookie,
    openai_api_key: settings.openai_api_key,
    openai_base_url: settings.openai_base_url,
    openai_model: settings.openai_model,
  }
  setRuntimeApiBaseUrl(targetApiBaseUrl)

  let backend: BackendRuntimeSettings
  try {
    backend = await updateBackendRuntimeSettings(updates)
  } catch (error) {
    setRuntimeApiBaseUrl(previousApiBaseUrl)
    throw error
  }

  const apiBaseUrl = setRuntimeApiBaseUrl(targetApiBaseUrl)
  const mapJsKey = setRuntimeMapJsKey(settings.vite_amap_web_js_key || backend.vite_amap_web_js_key)
  setRuntimeGoogleMapsApiKey(settings.google_maps_api_key || backend.google_maps_api_key)

  emitRuntimeSettingsUpdated()

  return {
    api_base_url: apiBaseUrl,
    ...backend,
    vite_amap_web_js_key: mapJsKey || backend.vite_amap_web_js_key,
  }
}

/**
 * 提交旅行规划任务（立即返回 task_id）
 */
export async function submitTripPlan(formData: TripFormData): Promise<SubmitTripPlanResponse> {
  try {
    const response = await apiClient.post('/api/trip/plan', formData)
    return response.data
  } catch (error: any) {
    console.error('提交旅行计划失败:', error)
    throw new Error(error.response?.data?.detail || error.message || t('api.submitTripPlanFailed'))
  }
}

/**
 * 轮询任务状态
 */
export async function pollTaskStatus(taskId: string): Promise<any> {
  try {
    const response = await apiClient.get(`/api/trip/status/${taskId}`)
    return response.data
  } catch (error: any) {
    console.error('查询任务状态失败:', error)
    throw new Error(error.response?.data?.detail || error.message || t('api.queryTaskStatusFailed'))
  }
}

export async function getTripHistory(limit = 8): Promise<TripHistoryItem[]> {
  try {
    const response = await apiClient.get<TripHistoryResponse>('/api/trip/history', {
      params: { limit },
    })
    return Array.isArray(response.data?.items) ? response.data.items : []
  } catch (error: any) {
    console.error('查询历史计划失败:', error)
    throw new Error(error.response?.data?.detail || error.message || t('api.queryTaskStatusFailed'))
  }
}

export async function deleteTripHistory(id: string): Promise<{ success: boolean }> {
  try {
    const response = await apiClient.delete(`/api/trip/history/${id}`)
    return response.data || { success: false }
  } catch (error: any) {
    console.error('删除旅行计划失败:', error)
    throw new Error(error.response?.data?.message || error.message || '删除计划失败')
  }
}

export async function getTripHistoryById(id: string): Promise<{ success: boolean; data?: any }> {
  try {
    const response = await apiClient.get(`/api/trip/history/${id}`)
    return response.data
  } catch (error: any) {
    console.error('获取旅行计划详情失败:', error)
    throw new Error(error.response?.data?.message || error.message || '获取计划详情失败')
  }
}

/**
 * 生成旅行计划（兼容旧接口，内部使用轮询）
 */
export async function generateTripPlan(
  formData: TripFormData,
  options?: GenerateTripPlanOptions
): Promise<TripPlanResponse> {
  const task = await submitTripPlan(formData)
  options?.onTaskCreated?.(task)

  return new Promise((resolve, reject) => {
    let settled = false
    const interval = setInterval(async () => {
      try {
        const status = await pollTaskStatus(task.task_id)
        const event: TripTaskEvent = {
          task_id: task.task_id,
          plan_id: status.plan_id || task.task_id,
          status: status.status,
          stage: status.stage,
          progress: status.progress,
          message: status.progress_text,
          result: status.result,
          error: status.error
        }
        options?.onTaskEvent?.(event)

        if (status.status === 'completed') {
          if (!settled) {
            settled = true
            clearInterval(interval)
            if (!status.result) {
              reject(new Error(t('api.generateTripPlanFailed')))
              return
            }
            resolve(status.result)
          }
          return
        }

        if (status.status === 'failed') {
          if (!settled) {
            settled = true
            clearInterval(interval)
            reject(new Error(status.error || status.message || t('api.generateTripPlanFailed')))
          }
        }
      } catch (err) {
        if (!settled) {
          settled = true
          clearInterval(interval)
          reject(err)
        }
      }
    }, 2000)
  })
}

/**
 * 健康检查
 */
export async function healthCheck(): Promise<any> {
  try {
    const response = await apiClient.get('/health')
    return response.data
  } catch (error: any) {
    console.error('健康检查失败:', error)
    throw new Error(error.message || t('api.healthCheckFailed'))
  }
}

/**
 * 管理后台 - 获取设置
 */
export async function getAdminSettings(): Promise<AdminSettings> {
  try {
    const response = await apiClient.get('/api/admin/settings')
    return response.data.data
  } catch (error: any) {
    console.error('获取设置失败:', error)
    throw new Error(error.response?.data?.message || error.message || '获取设置失败')
  }
}

/**
 * 管理后台 - 更新设置
 */
export async function updateAdminSettings(updates: Record<string, any>): Promise<any> {
  try {
    const response = await apiClient.post('/api/admin/settings', updates)
    return response.data
  } catch (error: any) {
    console.error('更新设置失败:', error)
    throw new Error(error.response?.data?.message || error.message || '更新设置失败')
  }
}

/**
 * 管理后台 - 获取详细健康检查
 */
export async function getAdminHealthDetails(): Promise<HealthCheckResponse> {
  try {
    const response = await apiClient.get('/api/admin/health/details')
    return response.data
  } catch (error: any) {
    console.error('获取健康检查失败:', error)
    throw new Error(error.response?.data?.message || error.message || '获取健康检查失败')
  }
}

export default apiClient

export async function getKnowledgeBaseList(): Promise<KnowledgeBaseItem[]> {
  try {
    const response = await apiClient.get('/api/knowledge')
    return Array.isArray(response.data?.data) ? response.data.data : []
  } catch (error: any) {
    console.error('获取知识库列表失败:', error)
    throw new Error(error.response?.data?.message || error.message || '获取知识库列表失败')
  }
}

export async function getKnowledgeBaseItem(id: string): Promise<KnowledgeBaseItem> {
  try {
    const response = await apiClient.get(`/api/knowledge/${id}`)
    return response.data?.data || {}
  } catch (error: any) {
    console.error('获取知识库条目失败:', error)
    throw new Error(error.response?.data?.message || error.message || '获取知识库条目失败')
  }
}

export async function searchKnowledgeBase(request: KnowledgeBaseSearchRequest): Promise<KnowledgeBaseItem[]> {
  try {
    const response = await apiClient.get('/api/knowledge/search', { params: request })
    return Array.isArray(response.data?.data) ? response.data.data : []
  } catch (error: any) {
    console.error('搜索知识库失败:', error)
    throw new Error(error.response?.data?.message || error.message || '搜索知识库失败')
  }
}

export async function createKnowledgeBase(request: KnowledgeBaseRequest): Promise<any> {
  try {
    const response = await apiClient.post('/api/knowledge', request)
    return response.data
  } catch (error: any) {
    console.error('创建知识库条目失败:', error)
    throw new Error(error.response?.data?.message || error.message || '创建知识库条目失败')
  }
}

export async function updateKnowledgeBase(id: string, request: KnowledgeBaseRequest): Promise<any> {
  try {
    const response = await apiClient.put(`/api/knowledge/${id}`, request)
    return response.data
  } catch (error: any) {
    console.error('更新知识库条目失败:', error)
    throw new Error(error.response?.data?.message || error.message || '更新知识库条目失败')
  }
}

export async function deleteKnowledgeBase(id: string): Promise<any> {
  try {
    const response = await apiClient.delete(`/api/knowledge/${id}`)
    return response.data
  } catch (error: any) {
    console.error('删除知识库条目失败:', error)
    throw new Error(error.response?.data?.message || error.message || '删除知识库条目失败')
  }
}

export async function getKnowledgeBaseAttractions(): Promise<string[]> {
  try {
    const response = await apiClient.get('/api/knowledge/attractions')
    return Array.isArray(response.data?.data) ? response.data.data : []
  } catch (error: any) {
    console.error('获取景区名称列表失败:', error)
    throw new Error(error.response?.data?.message || error.message || '获取景区名称列表失败')
  }
}

export async function getKnowledgeBaseCategories(): Promise<string[]> {
  try {
    const response = await apiClient.get('/api/knowledge/categories')
    return Array.isArray(response.data?.data) ? response.data.data : []
  } catch (error: any) {
    console.error('获取分类列表失败:', error)
    throw new Error(error.response?.data?.message || error.message || '获取分类列表失败')
  }
}

export async function knowledgeBaseQA(request: KnowledgeBaseQARequest): Promise<KnowledgeBaseQAResponse> {
  try {
    const response = await apiClient.post('/api/knowledge/qa', request)
    return response.data
  } catch (error: any) {
    console.error('知识库问答失败:', error)
    throw new Error(error.response?.data?.message || error.message || '知识库问答失败')
  }
}

export interface QAHistoryItem {
  id: string
  question: string
  answer: string
  attraction_name?: string
  references?: any[]
  create_time?: string
  update_time?: string
}

export async function getQAHistory(): Promise<QAHistoryItem[]> {
  try {
    const response = await apiClient.get('/api/knowledge/qa-history')
    return Array.isArray(response.data?.data) ? response.data.data : []
  } catch (error: any) {
    console.error('获取问答记录失败:', error)
    throw new Error(error.response?.data?.message || error.message || '获取问答记录失败')
  }
}

export async function saveQAHistory(data: { question: string; answer: string; attraction_name?: string; references?: any[] }): Promise<any> {
  try {
    const response = await apiClient.post('/api/knowledge/qa-history', data)
    return response.data
  } catch (error: any) {
    console.error('保存问答记录失败:', error)
    throw new Error(error.response?.data?.message || error.message || '保存问答记录失败')
  }
}

export async function clearQAHistory(): Promise<any> {
  try {
    const response = await apiClient.delete('/api/knowledge/qa-history')
    return response.data
  } catch (error: any) {
    console.error('清除问答记录失败:', error)
    throw new Error(error.response?.data?.message || error.message || '清除问答记录失败')
  }
}

export async function deleteQAHistory(id: string): Promise<any> {
  try {
    const response = await apiClient.delete(`/api/knowledge/qa-history/${id}`)
    return response.data
  } catch (error: any) {
    console.error('删除问答记录失败:', error)
    throw new Error(error.response?.data?.message || error.message || '删除问答记录失败')
  }
}


export async function getProvinceNames(): Promise<string[]> {
  try {
    const response = await apiClient.get('/api/region/province-names')
    return Array.isArray(response.data?.data) ? response.data.data : []
  } catch (error: any) {
    console.error('获取省份列表失败:', error)
    return []
  }
}

export async function getCityNamesByProvince(provinceName: string): Promise<string[]> {
  try {
    const response = await apiClient.get('/api/region/city-names', { params: { provinceName } })
    return Array.isArray(response.data?.data) ? response.data.data : []
  } catch (error: any) {
    console.error('获取城市列表失败:', error)
    return []
  }
}

export async function getDistrictNamesByCity(cityName: string): Promise<string[]> {
  try {
    const response = await apiClient.get('/api/region/district-names', { params: { cityName } })
    return Array.isArray(response.data?.data) ? response.data.data : []
  } catch (error: any) {
    console.error('获取区县列表失败:', error)
    return []
  }
}
