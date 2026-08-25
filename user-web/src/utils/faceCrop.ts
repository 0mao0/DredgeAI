/** 按 bbox [x1, y1, x2, y2]（原图像素坐标）从照片中裁出人脸，返回 JPEG Blob。 */
export async function cropFaceFromPhoto(photo: Blob, bbox: number[]): Promise<Blob> {
  const bitmap = await createImageBitmap(photo)
  try {
    const [rawX1, rawY1, rawX2, rawY2] = bbox
    const x1 = Math.max(0, Math.round(rawX1))
    const y1 = Math.max(0, Math.round(rawY1))
    const w = Math.max(1, Math.min(bitmap.width - x1, Math.round(rawX2) - x1))
    const h = Math.max(1, Math.min(bitmap.height - y1, Math.round(rawY2) - y1))
    const canvas = document.createElement('canvas')
    canvas.width = w
    canvas.height = h
    const ctx = canvas.getContext('2d')
    if (!ctx) throw new Error('canvas 不可用')
    ctx.drawImage(bitmap, x1, y1, w, h, 0, 0, w, h)
    return new Promise((resolve, reject) => {
      canvas.toBlob((b) => (b ? resolve(b) : reject(new Error('人脸裁剪失败'))), 'image/jpeg', 0.85)
    })
  } finally {
    bitmap.close()
  }
}
