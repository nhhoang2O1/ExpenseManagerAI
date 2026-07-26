package com.example.appquanlychitieu.ui.receipt;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNull;

import android.content.Context;

import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import org.junit.Test;
import org.junit.runner.RunWith;

/** A new store instance models restoration after the app process is recreated. */
@RunWith(AndroidJUnit4.class)
public class ReceiptDraftPersistenceInstrumentedTest {
    @Test
    public void draftSurvivesStoreRecreationAndCanBeCleared() {
        Context context = InstrumentationRegistry.getInstrumentation().getTargetContext();
        ReceiptDraftStore first = new ReceiptDraftStore(context);
        first.clear();
        first.save("receipt-7", "PROCESSING", "content://camera/7", "key-7", "QUEUED");

        ReceiptDraftStore.Draft restored = new ReceiptDraftStore(context).load();

        assertNotNull(restored);
        assertEquals("receipt-7", restored.receiptId);
        assertEquals("PROCESSING", restored.phase);
        assertEquals("content://camera/7", restored.imageUri);
        assertEquals("key-7", restored.idempotencyKey);
        assertEquals("QUEUED", restored.status);
        first.clear();
        assertNull(new ReceiptDraftStore(context).load());
    }
}
