import { Link, useRouterState } from '@tanstack/react-router'
import { Sparkles } from 'lucide-react'
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
  useSidebar,
} from '@/components/ui/sidebar'
import { useSession } from '@/lib/auth/session'
import { navSections } from './nav'

export function Logo({ collapsed = false }: { collapsed?: boolean }) {
  return (
    <span className="font-display flex items-center gap-2 text-lg font-bold tracking-tight">
      <span className="bg-primary text-primary-foreground flex size-8 shrink-0 items-center justify-center rounded-lg shadow-sm">
        <Sparkles className="size-4" aria-hidden />
      </span>
      {!collapsed && (
        <span>
          Insta<span className="text-primary">Vende</span>
        </span>
      )}
    </span>
  )
}

export function AppSidebar() {
  const pathname = useRouterState({ select: (s) => s.location.pathname })
  const { business } = useSession()
  const { state, setOpenMobile } = useSidebar()

  return (
    <Sidebar collapsible="icon" variant="sidebar">
      <SidebarHeader className="h-16 justify-center px-3">
        <Link to="/dashboard" aria-label="Ir al inicio">
          <Logo collapsed={state === 'collapsed'} />
        </Link>
      </SidebarHeader>
      <SidebarContent>
        {navSections.map((section) => (
          <SidebarGroup key={section.label}>
            <SidebarGroupLabel>{section.label}</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                {section.items.map((item) => {
                  const active = item.matchPrefix ? pathname.startsWith(item.matchPrefix) : pathname === item.to
                  return (
                    <SidebarMenuItem key={item.to}>
                      <SidebarMenuButton asChild isActive={active} tooltip={item.title}>
                        <Link to={item.to} onClick={() => setOpenMobile(false)}>
                          <item.icon aria-hidden />
                          <span>{item.title}</span>
                        </Link>
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  )
                })}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        ))}
      </SidebarContent>
      <SidebarFooter>
        {state === 'expanded' && (
          <div className="bg-sidebar-accent/60 rounded-lg p-3 text-xs">
            <p className="text-sidebar-accent-foreground truncate font-semibold">{business.name}</p>
            <p className="text-muted-foreground">{business.role === 'Admin' ? 'Administrador' : 'Miembro'}</p>
          </div>
        )}
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  )
}
