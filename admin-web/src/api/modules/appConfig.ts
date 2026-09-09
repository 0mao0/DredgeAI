import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { ApplicationConfiguration } from '@shared/core/types/appConfig'

/** 拉取当前用户应用配置（ABP ApplicationConfigurationDto） */
export function getApplicationConfiguration(): Promise<ApplicationConfiguration> {
  return request.get<ApplicationConfiguration>(urls.appConfig)
}
