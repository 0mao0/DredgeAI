import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { UserInfo } from '@/types'

interface AbpProfileDto {
  userName: string
  email: string
  name?: string | null
  surname?: string | null
  phoneNumber?: string | null
  concurrencyStamp?: string
  extraProperties?: Record<string, unknown>
}

export interface UpdateProfilePayload {
  userName: string
  email: string
  name: string
  phoneNumber?: string
  concurrencyStamp?: string
}

function mapProfile(dto: AbpProfileDto): UserInfo {
  const ep = dto.extraProperties ?? {}
  return {
    username: dto.userName,
    name: dto.name || dto.userName,
    email: dto.email,
    phone: dto.phoneNumber ?? undefined,
    // 容忍 extraProperties 键大小写（ABP JSON 序列化策略可能 camelCase）
    departments: (ep.DepartmentNames ?? ep.departmentNames ?? []) as string[],
    roles: (ep.RoleNames ?? ep.roleNames ?? []) as string[],
    createdAt: (ep.CreationTime ?? ep.creationTime) as string | undefined,
    lastLogin: (ep.LastLoginTime ?? ep.lastLoginTime) as string | undefined,
    concurrencyStamp: dto.concurrencyStamp,
  }
}

export async function getProfile(): Promise<UserInfo> {
  return mapProfile(await request.get<AbpProfileDto>(urls.adminProfile))
}

export async function updateProfile(input: UpdateProfilePayload): Promise<UserInfo> {
  return mapProfile(await request.put<AbpProfileDto>(urls.adminProfile, {
    userName: input.userName,
    email: input.email,
    name: input.name,
    phoneNumber: input.phoneNumber ?? null,
    concurrencyStamp: input.concurrencyStamp,
  }))
}
