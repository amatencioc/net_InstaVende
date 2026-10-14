import {
  BarChart3,
  Bell,
  Bot,
  Box,
  LayoutDashboard,
  MessagesSquare,
  Radio,
  Settings,
  ShoppingBag,
  type LucideIcon,
} from 'lucide-react'
import type { FileRoutesByTo } from '@/routeTree.gen'

export interface NavItem {
  title: string
  to: keyof FileRoutesByTo
  icon: LucideIcon
  /** Coincidencia por prefijo para marcar activo en sub-rutas. */
  matchPrefix?: string
}

export interface NavSection {
  label: string
  items: NavItem[]
}

/** Navegación principal del panel. */
export const navSections: NavSection[] = [
  {
    label: 'Principal',
    items: [
      { title: 'Inicio', to: '/dashboard', icon: LayoutDashboard },
      { title: 'Mensajes', to: '/inbox', icon: MessagesSquare, matchPrefix: '/inbox' },
      { title: 'Pedidos', to: '/orders', icon: ShoppingBag, matchPrefix: '/orders' },
      { title: 'Productos', to: '/products', icon: Box, matchPrefix: '/products' },
    ],
  },
  {
    label: 'Vendedor IA',
    items: [
      { title: 'Mi vendedor', to: '/seller', icon: Bot, matchPrefix: '/seller' },
      { title: 'Recordatorios', to: '/reminders', icon: Bell, matchPrefix: '/reminders' },
      { title: 'Métricas', to: '/metrics', icon: BarChart3 },
    ],
  },
  {
    label: 'Cuenta',
    items: [
      { title: 'Canales', to: '/channels', icon: Radio, matchPrefix: '/channels' },
      { title: 'Configuración', to: '/settings', icon: Settings, matchPrefix: '/settings' },
    ],
  },
]
