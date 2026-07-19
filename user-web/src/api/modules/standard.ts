import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { StandardResult, StandardSearchHistory, StandardCategory } from '@/types'

export function getStandardResult(): Promise<StandardResult[]> {
  return request.get<StandardResult[]>(urls.standardResult)
}

export function getStandardHistory(): Promise<StandardSearchHistory[]> {
  return request.get<StandardSearchHistory[]>(urls.standardHistory)
}

export function getStandardCategories(): Promise<StandardCategory[]> {
  return request.get<StandardCategory[]>(urls.standardCategories)
}

export function getRecommendedQuestions(): Promise<string[]> {
  return request.get<string[]>(urls.standardRecommended)
}
