import { createFileRoute, redirect } from '@tanstack/react-router'

export const Route = createFileRoute('/_app/seller/')({
  beforeLoad: () => {
    throw redirect({ to: '/seller/personality' })
  },
})
