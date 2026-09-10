import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { OrgUnit } from '@/types'

export function getOrgUnitTree(): Promise<OrgUnit[]> {
  return request.get<OrgUnit[]>(urls.orgUnitTree)
}

export function createOrgUnit(data: { displayName: string, parentId?: string | null }): Promise<OrgUnit> {
  return request.post<OrgUnit>(urls.orgUnits, data)
}

export function updateOrgUnit(id: string, data: { displayName?: string, parentId?: string }): Promise<OrgUnit> {
  return request.put<OrgUnit>(urls.orgUnitDetail.replace(':id', id), data)
}

export function deleteOrgUnit(id: string): Promise<void> {
  return request.delete(urls.orgUnitDetail.replace(':id', id))
}
