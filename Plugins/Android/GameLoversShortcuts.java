package com.gamelovers.mobileservices;

import android.app.Activity;
import android.content.Intent;
import android.content.pm.ShortcutInfo;
import android.content.pm.ShortcutManager;
import android.net.Uri;
import android.os.Build;

import java.util.ArrayList;
import java.util.List;

/** Publishes dynamic launcher shortcuts whose tap opens this app's activity with a deep-link URI. */
public final class GameLoversShortcuts {
    private GameLoversShortcuts() {
    }

    /** Replaces the app's dynamic shortcuts. Arrays are parallel; an empty subtitle means none. */
    public static void set(Activity activity, String[] ids, String[] titles, String[] subtitles, String[] uris) {
        if (Build.VERSION.SDK_INT < 25) {
            return;
        }

        ShortcutManager manager = activity.getSystemService(ShortcutManager.class);
        if (manager == null) {
            return;
        }

        List<ShortcutInfo> shortcuts = new ArrayList<>();
        for (int i = 0; i < ids.length; i++) {
            // An explicit component keeps the tap inside this app, and Unity reads the data URI as a deep link.
            Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(uris[i]));
            intent.setClassName(activity.getPackageName(), activity.getClass().getName());

            ShortcutInfo.Builder builder = new ShortcutInfo.Builder(activity, ids[i])
                    .setShortLabel(titles[i])
                    .setIntent(intent);
            if (subtitles[i] != null && !subtitles[i].isEmpty()) {
                builder.setLongLabel(subtitles[i]);
            }
            shortcuts.add(builder.build());
        }
        manager.setDynamicShortcuts(shortcuts);
    }

    /** Removes every dynamic shortcut the app published. */
    public static void clear(Activity activity) {
        if (Build.VERSION.SDK_INT < 25) {
            return;
        }

        ShortcutManager manager = activity.getSystemService(ShortcutManager.class);
        if (manager != null) {
            manager.removeAllDynamicShortcuts();
        }
    }
}
