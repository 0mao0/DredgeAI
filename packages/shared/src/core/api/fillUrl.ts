export function fillUrl(template: string, params: Record<string, string>): string {
  return Object.entries(params).reduce(
    (url, [key, value]) => url.split(`:${key}`).join(encodeURIComponent(value)),
    template,
  )
}
