import request from '@/api/request'
import type { StandardResult, StandardSearchHistory, StandardCategory } from '@/types'

export function getStandardResult(): Promise<StandardResult[]> {
  return request.get('/standard/result') as unknown as Promise<StandardResult[]>
}

export function getStandardHistory(): Promise<StandardSearchHistory[]> {
  return request.get('/standard/history') as unknown as Promise<StandardSearchHistory[]>
}

export function getStandardCategories(): Promise<StandardCategory[]> {
  return request.get('/standard/categories') as unknown as Promise<StandardCategory[]>
}

export function getRecommendedQuestions(): Promise<string[]> {
  return request.get('/standard/recommended') as unknown as Promise<string[]>
}
