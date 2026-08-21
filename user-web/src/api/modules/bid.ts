import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { BidReviewSession } from '@/types'

export function getBidSessions(): Promise<BidReviewSession[]> {
  return request.get<BidReviewSession[]>(urls.bidSessions)
}
