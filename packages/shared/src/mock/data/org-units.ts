import type { OrgUnit } from '@shared/types'

/** 组织单位树 mock：三层结构，code 对齐 ABP 层级编码约定 */
export const mockOrgUnits: OrgUnit[] = [
  {
    id: '1',
    code: '0001',
    displayName: '集团总部',
    parentId: null,
    creationTime: '2025-01-01T08:00:00',
    children: [
      {
        id: '2',
        code: '0001.0001',
        displayName: '工程部',
        parentId: '1',
        creationTime: '2025-01-05T08:00:00',
        children: [
          {
            id: '4',
            code: '0001.0001.0001',
            displayName: '项目部一组',
            parentId: '2',
            creationTime: '2025-02-01T08:00:00',
            children: [],
          },
        ],
      },
      {
        id: '3',
        code: '0001.0002',
        displayName: '市场部',
        parentId: '1',
        creationTime: '2025-01-10T08:00:00',
        children: [],
      },
    ],
  },
]
