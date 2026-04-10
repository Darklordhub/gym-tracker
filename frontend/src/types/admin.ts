export type AdminUser = {
  id: number
  email: string
  fullName: string
  displayName: string | null
  role: 'User' | 'Admin'
  isActive: boolean
  createdAt: string
}

export type UpdateAdminUserRolePayload = {
  role: AdminUser['role']
}

export type UpdateAdminUserStatusPayload = {
  isActive: boolean
}
