/** 组织单位（树形节点，对应后端 OrganizationUnitDto） */
export interface OrgUnit {
  id: string
  /** 层级编码（ABP 自动生成，如 "0001.0002"） */
  code: string
  displayName: string
  /** 上级组织 ID，null 表示根节点 */
  parentId: string | null
  /** 创建时间（UTC ISO 串） */
  creationTime: string
  children: OrgUnit[]
}
