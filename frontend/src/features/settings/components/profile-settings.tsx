import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { Loader2 } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { Alert } from '@/components/shared/alert'
import { QueryError } from '@/components/shared/query-state'
import { Button } from '@/components/ui/button'
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { ApiError } from '@/lib/api/client'
import { formatDate, formatRelative, initials } from '@/lib/format'
import { profileQuery, useDeleteAvatar, useUpdateProfile, useUploadAvatar, type SettingsProfile } from '../api'
import { nullIfEmpty, profileSchema, type ProfileFormValues } from '../schemas'
import { ImageDropzone } from './image-dropzone'
import { SettingsCard, SettingsCardSkeleton } from './settings-card'

export function ProfileSettings() {
  const profile = useQuery(profileQuery)

  if (profile.isPending)
    return (
      <div className="space-y-6">
        <SettingsCardSkeleton rows={1} />
        <SettingsCardSkeleton rows={3} />
      </div>
    )
  if (profile.isError) return <QueryError error={profile.error} onRetry={() => void profile.refetch()} />

  return (
    <div className="space-y-6">
      <AvatarCard profile={profile.data} />
      <ProfileForm profile={profile.data} />
    </div>
  )
}

function AvatarCard({ profile }: { profile: SettingsProfile }) {
  const upload = useUploadAvatar()
  const remove = useDeleteAvatar()
  const fullName = `${profile.firstName} ${profile.lastName}`.trim()

  return (
    <SettingsCard title="Foto de perfil" description="Se muestra en el menú de usuario y en la lista del equipo.">
      <ImageDropzone
        label="foto de perfil"
        imageUrl={profile.avatarUrl}
        fallback={
          <span className="text-primary text-2xl font-semibold" aria-hidden>
            {initials(fullName || profile.email)}
          </span>
        }
        uploading={upload.isPending}
        removing={remove.isPending}
        onUpload={(file) => upload.mutateAsync(file).catch(() => undefined)}
        onRemove={() => remove.mutate()}
      />
    </SettingsCard>
  )
}

function ProfileForm({ profile }: { profile: SettingsProfile }) {
  const update = useUpdateProfile()
  const form = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    values: { firstName: profile.firstName, lastName: profile.lastName, phoneNumber: profile.phoneNumber ?? '' },
    resetOptions: { keepDirtyValues: true },
  })

  const onSubmit = form.handleSubmit((values) =>
    update.mutate(
      { firstName: values.firstName, lastName: values.lastName, phoneNumber: nullIfEmpty(values.phoneNumber) },
      {
        onSuccess: (saved) =>
          form.reset({ firstName: saved.firstName, lastName: saved.lastName, phoneNumber: saved.phoneNumber ?? '' }),
        onError: (error) => {
          if (error instanceof ApiError)
            for (const [field, message] of Object.entries(error.fieldErrors))
              form.setError(field as keyof ProfileFormValues, { message })
        },
      },
    ),
  )

  const fieldErrors = update.error instanceof ApiError ? Object.keys(update.error.fieldErrors).length : 0

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} noValidate>
        <SettingsCard
          title="Información personal"
          description={
            <>
              Miembro desde el {formatDate(profile.createdAt)}
              {profile.lastLoginAt && <> · último acceso {formatRelative(profile.lastLoginAt)}</>}
            </>
          }
          footer={
            <>
              <Button
                type="button"
                variant="ghost"
                disabled={!form.formState.isDirty || update.isPending}
                onClick={() => form.reset()}
              >
                Descartar
              </Button>
              <Button type="submit" disabled={!form.formState.isDirty || update.isPending}>
                {update.isPending && <Loader2 className="animate-spin" />}
                Guardar cambios
              </Button>
            </>
          }
        >
          <div className="space-y-4">
            {update.error && fieldErrors === 0 && <Alert tone="destructive">{update.error.message}</Alert>}
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="firstName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Nombre</FormLabel>
                    <FormControl>
                      <Input autoComplete="given-name" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="lastName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Apellido</FormLabel>
                    <FormControl>
                      <Input autoComplete="family-name" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="phoneNumber"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Teléfono</FormLabel>
                    <FormControl>
                      <Input type="tel" autoComplete="tel" placeholder="+51 999 888 777" {...field} />
                    </FormControl>
                    <FormDescription>Opcional. Visible para tu equipo.</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <div className="grid content-start gap-2">
                <label htmlFor="profile-email" className="text-sm font-medium">
                  Email de acceso
                </label>
                <Input id="profile-email" value={profile.email} readOnly disabled />
                <p className="text-muted-foreground text-sm">
                  Para cambiarlo ve a{' '}
                  <Link to="/settings/security" className="text-primary font-medium hover:underline">
                    Seguridad
                  </Link>
                  .
                </p>
              </div>
            </div>
          </div>
        </SettingsCard>
      </form>
    </Form>
  )
}
