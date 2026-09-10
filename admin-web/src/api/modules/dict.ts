import request from '@/api/request'
import { urls } from '@shared/core/api'

// ---- 字典类型 ----

/** 字典类型树节点（对应后端 DictTypeTreeNodeDto） */
export interface DictTypeNode {
  id: string
  name: string
  code: string
  parentId: string | null
  isStatic: boolean
  children: DictTypeNode[]
}

/** 字典类型详情（对应后端 DictTypeDto，编辑回填用） */
export interface DictTypeDetail {
  id: string
  name: string
  code: string
  fullCode: string
  parentId: string | null
  moduleCode: string | null
  sort: number
  remark: string | null
  isStatic: boolean
  creationTime: string
}

export interface DictTypeFormData {
  name: string
  /** 创建时留空则由后端自动生成 */
  code?: string
  parentId?: string | null
  moduleCode?: string
  sort: number
  remark?: string
}

export function getDictTypeTree(): Promise<DictTypeNode[]> {
  return request.get<DictTypeNode[]>(urls.dictTypeTree)
}

export function getDictType(id: string): Promise<DictTypeDetail> {
  return request.get<DictTypeDetail>(urls.dictTypeDetail.replace(':id', id))
}

export function createDictType(data: DictTypeFormData): Promise<DictTypeDetail> {
  const payload: Record<string, unknown> = { ...data }
  if (!payload.code) delete payload.code
  return request.post<DictTypeDetail>(urls.dictTypes, payload)
}

export function updateDictType(id: string, data: DictTypeFormData): Promise<DictTypeDetail> {
  const payload: Record<string, unknown> = { ...data }
  if (!payload.code) delete payload.code
  return request.put<DictTypeDetail>(urls.dictTypeDetail.replace(':id', id), payload)
}

/** 删除字典类型；cascade=true 级联删除所有子类型及其字典数据 */
export function deleteDictType(id: string, cascade: boolean): Promise<void> {
  return request.delete(urls.dictTypeDetail.replace(':id', id), { params: { cascade } })
}

// ---- 字典数据 ----

/** 字典数据列表项（对应后端 DictDataDto） */
export interface DictDataItem {
  id: string
  typeId: string
  parentId: string | null
  code: string
  value: string
  name: string
  sort: number
  isEnabled: boolean
  remark: string | null
  isStatic: boolean
}

/** 字典数据树节点（对应后端 DictDataTreeNodeDto）：右侧树形列表与表单“上级数据”选项共用 */
export interface DictDataNode {
  id: string
  value: string
  name: string
  code: string
  parentId: string | null
  sort: number
  isEnabled: boolean
  remark: string | null
  isStatic: boolean
  children: DictDataNode[]
}

export interface DictDataFormData {
  typeId?: string
  parentId?: string | null
  value: string
  name: string
  sort: number
  isEnabled: boolean
  remark?: string
}

export function createDictData(data: DictDataFormData): Promise<DictDataItem> {
  return request.post<DictDataItem>(urls.dictData, data)
}

export function updateDictData(id: string, data: DictDataFormData): Promise<DictDataItem> {
  return request.put<DictDataItem>(urls.dictDataDetail.replace(':id', id), data)
}

export function deleteDictData(id: string): Promise<void> {
  return request.delete(urls.dictDataDetail.replace(':id', id))
}

/** 按字典类型取字典数据树（表单“上级数据”选项用） */
export function getDictDataTree(typeId: string): Promise<DictDataNode[]> {
  return request.get<DictDataNode[]>(urls.dictDataTree.replace(':typeId', typeId))
}
