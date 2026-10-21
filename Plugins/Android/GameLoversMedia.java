package com.gamelovers.mobileservices;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.media.MediaScannerConnection;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.MediaStore;
import android.webkit.MimeTypeMap;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;

/** Saves a file to the shared gallery without any storage permission on Android 10 and newer. */
public final class GameLoversMedia {
    /** Receives the outcome on a background thread: a non-null value means success, null means failure. */
    public interface ResultCallback {
        void onResult(String result);
    }

    private GameLoversMedia() {
    }

    /** Copies {@code path} into the shared Pictures or Movies collection under {@code album} on a worker thread. */
    public static void saveToGallery(final Activity activity, final String path, final boolean video,
                                     final String album, final ResultCallback callback) {
        new Thread(new Runnable() {
            @Override
            public void run() {
                String result = null;
                try {
                    result = save(activity, new File(path), video, album);
                } catch (Exception e) {
                    android.util.Log.w("GameLovers", "SaveToGallery failed: " + e.getMessage());
                }
                callback.onResult(result);
            }
        }, "GameLovers-SaveToGallery").start();
    }

    private static String save(Activity activity, File source, boolean video, String album) throws IOException {
        if (!source.isFile()) {
            return null;
        }

        String mimeType = guessMimeType(source.getName(), video);
        String directory = video ? Environment.DIRECTORY_MOVIES : Environment.DIRECTORY_PICTURES;

        if (Build.VERSION.SDK_INT >= 29) {
            ContentResolver resolver = activity.getContentResolver();
            ContentValues values = new ContentValues();
            values.put(MediaStore.MediaColumns.DISPLAY_NAME, source.getName());
            values.put(MediaStore.MediaColumns.MIME_TYPE, mimeType);
            values.put(MediaStore.MediaColumns.RELATIVE_PATH, directory + "/" + album);
            values.put(MediaStore.MediaColumns.IS_PENDING, 1);

            Uri collection = video
                    ? MediaStore.Video.Media.getContentUri(MediaStore.VOLUME_EXTERNAL_PRIMARY)
                    : MediaStore.Images.Media.getContentUri(MediaStore.VOLUME_EXTERNAL_PRIMARY);
            Uri item = resolver.insert(collection, values);
            if (item == null) {
                return null;
            }

            try (InputStream in = new FileInputStream(source); OutputStream out = resolver.openOutputStream(item)) {
                if (out == null) {
                    resolver.delete(item, null, null);
                    return null;
                }
                copy(in, out);
            } catch (IOException e) {
                resolver.delete(item, null, null);
                throw e;
            }

            values.clear();
            values.put(MediaStore.MediaColumns.IS_PENDING, 0);
            resolver.update(item, values, null, null);
            return item.toString();
        }

        File folder = new File(Environment.getExternalStoragePublicDirectory(directory), album);
        if (!folder.isDirectory() && !folder.mkdirs()) {
            return null;
        }

        File destination = uniqueFile(folder, source.getName());
        try (InputStream in = new FileInputStream(source); OutputStream out = new FileOutputStream(destination)) {
            copy(in, out);
        }
        MediaScannerConnection.scanFile(activity, new String[]{destination.getAbsolutePath()}, new String[]{mimeType}, null);
        return destination.getAbsolutePath();
    }

    private static void copy(InputStream in, OutputStream out) throws IOException {
        byte[] buffer = new byte[64 * 1024];
        int read;
        while ((read = in.read(buffer)) > 0) {
            out.write(buffer, 0, read);
        }
        out.flush();
    }

    private static File uniqueFile(File folder, String name) {
        File candidate = new File(folder, name);
        int dot = name.lastIndexOf('.');
        String stem = dot > 0 ? name.substring(0, dot) : name;
        String extension = dot > 0 ? name.substring(dot) : "";
        for (int i = 1; candidate.exists(); i++) {
            candidate = new File(folder, stem + "-" + i + extension);
        }
        return candidate;
    }

    private static String guessMimeType(String fileName, boolean video) {
        int dot = fileName.lastIndexOf('.');
        String extension = dot >= 0 ? fileName.substring(dot + 1).toLowerCase() : "";
        String mimeType = MimeTypeMap.getSingleton().getMimeTypeFromExtension(extension);
        return mimeType != null ? mimeType : (video ? "video/mp4" : "image/png");
    }
}
