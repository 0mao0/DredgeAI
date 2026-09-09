/** ABP 应用配置（GET /api/base/application-configuration 响应） */
export interface AppConfigCurrentUser {
  isAuthenticated: boolean
  id: string | null
  tenantId: string | null
  impersonatorUserId: string | null
  impersonatorTenantId: string | null
  impersonatorUserName: string | null
  impersonatorTenantName: string | null
  userName: string | null
  name: string | null
  surName: string | null
  email: string | null
  emailVerified: boolean
  phoneNumber: string | null
  phoneNumberVerified: boolean
  roles: string[] | null
  sessionId: string | null
}

export interface AppConfigLanguage {
  cultureName: string | null
  uiCultureName: string | null
  displayName: string | null
  twoLetterISOLanguageName: string | null
}

export interface AppConfigLocalization {
  currentCulture: {
    cultureName: string | null
    name: string | null
    displayName: string | null
    isRightToLeft: boolean
  } | null
  languages: AppConfigLanguage[] | null
  defaultResourceName: string | null
}

export interface ApplicationConfiguration {
  localization: AppConfigLocalization
  auth: { grantedPolicies: Record<string, boolean> | null }
  setting: { values: Record<string, string | null> | null }
  currentUser: AppConfigCurrentUser
  features: { values: Record<string, string | null> | null }
  globalFeatures: { enabledFeatures: string[] | null }
  multiTenancy: { isEnabled: boolean }
  currentTenant: { id: string | null, name: string | null, isAvailable: boolean } | null
  timing: { timeZone: { iana: { timeZoneName: string | null }, windows: { timeZoneId: string | null } } | null }
  clock: { kind: string | null }
  objectExtensions?: Record<string, unknown> | null
  extraProperties?: Record<string, unknown> | null
}
