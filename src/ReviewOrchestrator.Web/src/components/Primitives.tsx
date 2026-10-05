import type { ReactNode } from 'react'
import { ChevronDown, Inbox, LoaderCircle } from 'lucide-react'
import { initials } from '../lib/format'

export function Card({ title, subtitle, icon, actions, children, className = '' }: {
  title?: ReactNode
  subtitle?: ReactNode
  icon?: ReactNode
  actions?: ReactNode
  children: ReactNode
  className?: string
}) {
  return (
    <section className={`rounded-2xl border border-slate-200/80 bg-white shadow-sm shadow-slate-900/[0.03] ${className}`}>
      {title && (
        <header className="flex items-start justify-between gap-3 px-5 pt-4">
          <div className="flex items-start gap-2.5">
            {icon}
            <div>
              <h3 className="text-sm font-semibold text-slate-900">{title}</h3>
              {subtitle && <p className="mt-0.5 text-xs text-slate-500">{subtitle}</p>}
            </div>
          </div>
          {actions}
        </header>
      )}
      <div className="px-5 pt-3 pb-5">{children}</div>
    </section>
  )
}

export function MetricCard({ label, value, hint, icon, tone = 'default' }: {
  label: string
  value: ReactNode
  hint?: ReactNode
  icon?: ReactNode
  tone?: 'default' | 'warning'
}) {
  return (
    <div className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm shadow-slate-900/[0.03]">
      <div className="flex items-center justify-between text-xs font-medium text-slate-500">
        {label}
        <span className={tone === 'warning' ? 'text-amber-600' : 'text-slate-400'}>{icon}</span>
      </div>
      <div className="mt-2 text-2xl font-semibold tracking-tight text-slate-900 tabular-nums">{value}</div>
      {hint && <div className="mt-1 text-xs text-slate-500">{hint}</div>}
    </div>
  )
}

export function EmptyState({ title, message, icon }: { title: string; message?: string; icon?: ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center px-6 py-16 text-center">
      <div className="mb-3 rounded-full bg-slate-100 p-3 text-slate-400">{icon ?? <Inbox className="size-5" />}</div>
      <p className="text-sm font-semibold text-slate-800">{title}</p>
      {message && <p className="mt-1 max-w-sm text-sm text-slate-500">{message}</p>}
    </div>
  )
}

export function Spinner({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-2 py-16 text-sm text-slate-500">
      <LoaderCircle className="size-4 animate-spin" /> {label}
    </div>
  )
}

const avatarColors = ['bg-indigo-500', 'bg-emerald-500', 'bg-amber-500', 'bg-rose-500', 'bg-sky-500', 'bg-violet-500', 'bg-teal-500', 'bg-fuchsia-500']

export function ReviewerAvatar({ name, size = 'md' }: { name: string; size?: 'sm' | 'md' | 'lg' }) {
  const hash = [...name].reduce((h, c) => (h * 31 + c.charCodeAt(0)) >>> 0, 7)
  const sizes = { sm: 'size-6 text-[10px]', md: 'size-8 text-xs', lg: 'size-10 text-sm' }
  return (
    <span
      title={name}
      className={`inline-flex shrink-0 items-center justify-center rounded-full font-semibold text-white ${avatarColors[hash % avatarColors.length]} ${sizes[size]}`}
    >
      {initials(name)}
    </span>
  )
}

/** Small capacity meter: filled segments = active reviews. */
export function ReviewerCapacity({ active, capacity, inactive = false }: { active: number; capacity: number; inactive?: boolean }) {
  const full = active >= capacity
  return (
    <div className="flex items-center gap-2" title={`${active} of ${capacity} review slots in use`}>
      <div className="flex gap-0.5">
        {Array.from({ length: capacity }, (_, i) => (
          <span
            key={i}
            className={`h-1.5 w-4 rounded-full ${
              inactive ? 'bg-slate-200' : i < active ? (full ? 'bg-red-500' : 'bg-indigo-500') : 'bg-slate-200'
            }`}
          />
        ))}
      </div>
      <span className={`text-xs tabular-nums ${full && !inactive ? 'font-semibold text-red-600' : 'text-slate-500'}`}>
        {inactive ? 'inactive' : `${active} / ${capacity}`}
      </span>
    </div>
  )
}

export function FilterDropdown<T extends string>({ label, value, options, onChange }: {
  label: string
  value: T
  options: { value: T; label: string }[]
  onChange: (value: T) => void
}) {
  return (
    <label className="relative inline-flex items-center">
      <span className="sr-only">{label}</span>
      <select
        value={value}
        onChange={(e) => onChange(e.target.value as T)}
        className="h-9 appearance-none rounded-lg border border-slate-200 bg-white pr-8 pl-3 text-sm text-slate-700 shadow-sm outline-none hover:border-slate-300 focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100"
      >
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
      <ChevronDown className="pointer-events-none absolute right-2.5 size-4 text-slate-400" />
    </label>
  )
}

export function Button({ variant = 'secondary', children, className = '', ...props }: React.ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'secondary' | 'success' | 'warning' | 'ghost'
}) {
  const variants = {
    primary: 'bg-indigo-600 text-white shadow-sm hover:bg-indigo-500 disabled:bg-indigo-300',
    success: 'bg-emerald-600 text-white shadow-sm hover:bg-emerald-500 disabled:bg-emerald-300',
    warning: 'bg-white text-amber-800 ring-1 ring-inset ring-amber-300 hover:bg-amber-50 disabled:opacity-50',
    secondary: 'bg-white text-slate-700 ring-1 ring-inset ring-slate-200 shadow-sm hover:bg-slate-50 disabled:opacity-50',
    ghost: 'text-slate-600 hover:bg-slate-100 disabled:opacity-50',
  }
  return (
    <button
      {...props}
      className={`inline-flex h-9 items-center justify-center gap-1.5 rounded-lg px-3.5 text-sm font-medium transition-colors disabled:cursor-not-allowed ${variants[variant]} ${className}`}
    >
      {children}
    </button>
  )
}
