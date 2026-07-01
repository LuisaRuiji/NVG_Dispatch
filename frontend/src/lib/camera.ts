import { Camera, CameraResultType, CameraSource } from "@capacitor/camera";
import { Capacitor } from "@capacitor/core";

export async function captureDocumentPhoto(): Promise<File | null> {
  if (!Capacitor.isNativePlatform()) {
    return null;
  }

  const photo = await Camera.getPhoto({
    quality: 85,
    allowEditing: false,
    resultType: CameraResultType.DataUrl,
    source: CameraSource.Camera,
    promptLabelHeader: "Document Photo",
    promptLabelPhoto: "Choose from library",
    promptLabelPicture: "Take photo"
  });

  if (!photo.dataUrl) {
    return null;
  }

  const res = await fetch(photo.dataUrl);
  const blob = await res.blob();
  return new File([blob], `document-${Date.now()}.jpg`, { type: "image/jpeg" });
}
