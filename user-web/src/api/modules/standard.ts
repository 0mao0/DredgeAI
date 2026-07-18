import request from '@/api/request'
import type { StandardResult, StandardSearchHistory, StandardCategory } from '@/types'

export function getStandardResult(): Promise<StandardResult[]> {
  return request.get<StandardResult[]>('/standard/result')
}

export function getStandardHistory(): Promise<StandardSearchHistory[]> {
  return request.get<StandardSearchHistory[]>('/standard/history')
}

export function getStandardCategories(): Promise<StandardCategory[]> {
  return request.get<StandardCategory[]>('/standard/categories')
}

export function getRecommendedQuestions(): Promise<string[]> {
  return request.get<string[]>('/standard/recommended')
}
