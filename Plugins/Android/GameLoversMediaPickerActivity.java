package com.gamelovers.mobileservices;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.provider.MediaStore;
import android.webkit.MimeTypeMap;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.UUID;

/**
 * Transparent host for the system image picker. UnityPlayerActivity cannot receive activity results, so this
 * activity launches the picker, copies the chosen image into the app cache, reports the path, and finishes.
 * Needs no permission: the Android 13 photo picker and the document picker grant access to the chosen item only.
 */
public class GameLoversMediaPickerActivity extends Activity {
    /** Receives the copied file path on a background thread, or null when the player cancelled or the copy failed. */
    public interface PathCallback {
        void onPicked(String path);
    }

    private static final int RequestPick = 7141;
    private static final Object Lock = new Object();
    private static PathCallback pending;

    /** Starts the picker; reports null immediately when another pick is still open. */
    public static void start(Activity host, PathCallback callback) {
        synchronized (Lock) {
            if (pending != null) {
                callback.onPicked(null);
                return;
            }
            pending = callback;
        }
        host.startActivity(new Intent(host, GameLoversMediaPickerActivity.class));
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (savedInstanceState != null) {
            // Recreated after the process was killed: the original request is gone.
            finish();
            return;
        }

        Intent intent;
        if (Build.VERSION.SDK_INT >= 33) {
            intent = new Intent(MediaStore.ACTION_PICK_IMAGES);
        } else {
            intent = new Intent(Intent.ACTION_GET_CONTENT);
            intent.addCategory(Intent.CATEGORY_OPENABLE);
        }
        intent.setType("image/*");

        try {
            startActivityForResult(intent, RequestPick);
        } catch (Exception e) {
            complete(null);
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, final Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != RequestPick) {
            return;
        }

        if (resultCode != RESULT_OK || data == null || data.getData() == null) {
            complete(null);
            return;
        }

        final Uri uri = data.getData();
        new Thread(new Runnable() {
            @Override
            public void run() {
                complete(copyToCache(uri));
            }
        }, "GameLovers-PickImage").start();
    }

    private String copyToCache(Uri uri) {
        ContentResolver resolver = getContentResolver();
        String extension = MimeTypeMap.getSingleton().getExtensionFromMimeType(resolver.getType(uri));
        File destination = new File(getCacheDir(), "gamelovers-picked-" + UUID.randomUUID() + "." + (extension != null ? extension : "img"));
        try (InputStream in = resolver.openInputStream(uri); OutputStream out = new FileOutputStream(destination)) {
            if (in == null) {
                return null;
            }
            byte[] buffer = new byte[64 * 1024];
            int read;
            while ((read = in.read(buffer)) > 0) {
                out.write(buffer, 0, read);
            }
            return destination.getAbsolutePath();
        } catch (Exception e) {
            android.util.Log.w("GameLovers", "PickImage failed to copy the selection: " + e.getMessage());
            return null;
        }
    }

    private void complete(final String path) {
        PathCallback callback;
        synchronized (Lock) {
            callback = pending;
            pending = null;
        }

        final PathCallback target = callback;
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                finish();
                if (target != null) {
                    target.onPicked(path);
                }
            }
        });
    }
}
