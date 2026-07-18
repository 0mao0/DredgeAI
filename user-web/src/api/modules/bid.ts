import request from '@/api/request'
import type { BidReviewStep, RiskItem, BidReviewSession } from '@/types'

export function getBidSteps(): Promise<BidReviewStep[]> {
  return request.get<BidReviewStep[]>('/bid/steps')
}

export function getBidRisks(): Promise<RiskItem[]> {
  return request.get<RiskItem[]>('/bid/risks')
}

export function getBidSessions(): Promise<BidReviewSession[]> {
  return request.get<BidReviewSession[]>('/bid/sessions')
}

export function getBidDocument(): Promise<string> {
  return request.get<string>('/bid/document')
}
