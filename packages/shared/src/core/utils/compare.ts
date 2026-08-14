export const MAX_BID_DOCUMENTS = 8

const BID_LETTERS = 'ABCDEFGH'

export interface DocLabelSource {
  id: string
  role?: string
  fileName: string
}

const SELF_LETTER_RE = /(?:^|[^A-Z0-9])([A-H])(?=$|[^A-Z0-9])/i

function extractSelfLetter(fileName: string): string | null {
  const stem = fileName.replace(/\.[^.]+$/, '').trim()
  const match = stem.match(SELF_LETTER_RE)
  return match ? match[1].toUpperCase() : null
}

export function buildDocLabels(documents: DocLabelSource[]): Record<string, string> {
  const labels: Record<string, string> = {}
  const claimed = new Set<string>()
  const claimCounts = new Map<string, number>()
  const unclaimed: DocLabelSource[] = []

  for (const doc of documents) {
    if (doc.role === 'tender') {
      labels[doc.id] = '招标'
      continue
    }
    const letter = extractSelfLetter(doc.fileName)
    if (letter) {
      const count = (claimCounts.get(letter) ?? 0) + 1
      claimCounts.set(letter, count)
      claimed.add(letter)
      labels[doc.id] = count === 1 ? letter : `${letter}${count - 1}`
    } else {
      unclaimed.push(doc)
    }
  }

  let next = 0
  for (const doc of unclaimed) {
    while (next < BID_LETTERS.length && claimed.has(BID_LETTERS[next])) next++
    labels[doc.id] = next < BID_LETTERS.length ? BID_LETTERS[next] : doc.id
    next++
  }
  return labels
}

export function docLabel(documents: DocLabelSource[], docId: string): string {
  return buildDocLabels(documents)[docId] ?? docId
}

export function overviewDocLabels(docIds: string[], documents: DocLabelSource[]): string[] {
  return docIds.map((docId) => docLabel(documents, docId))
}
