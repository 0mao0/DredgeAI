import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { StandardResult, StandardSearchHistory, StandardCategory, StandardListItem, StandardProperty, StandardDocument, StandardAIAnalysis } from '@/types'

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

export function getStandardList(): Promise<StandardListItem[]> {
  return request.get<StandardListItem[]>(urls.standardList)
}

export function getStandardProperty(id: string): Promise<StandardProperty> {
  return request.get<StandardProperty>(urls.standardProperty, { params: { id } })
}

export function getStandardPropertyList(): Promise<StandardProperty[]> {
  return request.get<StandardProperty[]>(urls.standardPropertyList)
}

export function getStandardDocument(id: string): Promise<StandardDocument> {
  return request.get<StandardDocument>(urls.standardDocument, { params: { id } })
}

export function getStandardAIAnalysis(id: string): Promise<StandardAIAnalysis> {
  return request.get<StandardAIAnalysis>(urls.standardAIAnalysis, { params: { id } })
}

export function updateStandardProperty(id: string, data: Partial<StandardProperty>): Promise<void> {
  return request.put(urls.standardProperty, data, { params: { id } })
}
