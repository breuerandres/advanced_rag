import { BookOpen, Compass, FileText, ListChecks, ShieldCheck, type LucideIcon } from 'lucide-react'

export function typeTintIndex(documentType: string): number {
  let sum = 0
  for (const char of documentType) {
    sum += char.charCodeAt(0)
  }
  return sum % 6
}

export function typeIcon(documentType: string): LucideIcon {
  const normalized = documentType.toLowerCase()
  if (normalized.includes('manual')) {
    return BookOpen
  }
  if (normalized.includes('polit') || normalized.includes('polít') || normalized.includes('policy')) {
    return ShieldCheck
  }
  if (normalized.includes('proced')) {
    return ListChecks
  }
  if (normalized.includes('guia') || normalized.includes('guía') || normalized.includes('guide')) {
    return Compass
  }
  return FileText
}
