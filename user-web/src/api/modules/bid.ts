import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { BidReviewStep, RiskItem, BidReviewSession } from '@/types'

export function getBidSteps(): Promise<BidReviewStep[]> {
  return request.get<BidReviewStep[]>(urls.bidSteps)
}

export function getBidRisks(): Promise<RiskItem[]> {
  return request.get<RiskItem[]>(urls.bidRisks)
}

export function getBidSessions(): Promise<BidReviewSession[]> {
  return request.get<BidReviewSession[]>(urls.bidSessions)
}

export function getBidDocument(): Promise<string> {
  return request.get<string>(urls.bidDocument)
}
