import request from '@/api/request'
import type { BidReviewStep, RiskItem, BidReviewSession } from '@/types'

export function getBidSteps(): Promise<BidReviewStep[]> {
  return request.get('/bid/steps') as unknown as Promise<BidReviewStep[]>
}

export function getBidRisks(): Promise<RiskItem[]> {
  return request.get('/bid/risks') as unknown as Promise<RiskItem[]>
}

export function getBidSessions(): Promise<BidReviewSession[]> {
  return request.get('/bid/sessions') as unknown as Promise<BidReviewSession[]>
}

export function getBidDocument(): Promise<string> {
  return request.get('/bid/document') as unknown as Promise<string>
}
