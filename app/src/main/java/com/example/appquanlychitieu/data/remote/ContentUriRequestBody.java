package com.example.appquanlychitieu.data.remote;

import android.content.ContentResolver;
import android.content.Context;
import android.content.res.AssetFileDescriptor;
import android.net.Uri;

import androidx.annotation.Nullable;

import java.io.IOException;
import java.io.InputStream;

import okhttp3.MediaType;
import okhttp3.RequestBody;
import okio.BufferedSink;

public class ContentUriRequestBody extends RequestBody {
    private final ContentResolver contentResolver;
    private final Uri uri;
    private final MediaType mediaType;

    public ContentUriRequestBody(Context context, Uri uri) {
        contentResolver = context.getContentResolver();
        this.uri = uri;
        String mimeType = contentResolver.getType(uri);
        mediaType = MediaType.parse(mimeType == null ? "image/jpeg" : mimeType);
    }

    @Nullable
    @Override
    public MediaType contentType() {
        return mediaType;
    }

    @Override
    public long contentLength() {
        try (AssetFileDescriptor descriptor =
                     contentResolver.openAssetFileDescriptor(uri, "r")) {
            return descriptor == null ? -1L : descriptor.getLength();
        } catch (IOException ignored) {
            return -1L;
        }
    }

    @Override
    public void writeTo(BufferedSink sink) throws IOException {
        try (InputStream stream = contentResolver.openInputStream(uri)) {
            if (stream == null) {
                throw new IOException("Cannot open selected image");
            }
            byte[] buffer = new byte[8192];
            int read;
            while ((read = stream.read(buffer)) != -1) {
                sink.write(buffer, 0, read);
            }
        }
    }
}
