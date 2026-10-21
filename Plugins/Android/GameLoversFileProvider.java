package com.gamelovers.mobileservices;

import androidx.core.content.FileProvider;

/**
 * Package-owned FileProvider subclass. A unique class name lets the manifest declare it next to any FileProvider
 * that the game or another SDK already registers, without a manifest merge conflict.
 */
public class GameLoversFileProvider extends FileProvider {
}
