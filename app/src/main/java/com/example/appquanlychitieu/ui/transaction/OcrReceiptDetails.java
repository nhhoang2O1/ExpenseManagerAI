package com.example.appquanlychitieu.ui.transaction;

import android.content.Context;
import android.graphics.Typeface;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

import com.example.appquanlychitieu.data.model.Transaction;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.ReceiptDto;
import com.example.appquanlychitieu.data.remote.dto.OcrLineDto;
import com.example.appquanlychitieu.data.repository.ReceiptRepository;
import com.google.android.material.dialog.MaterialAlertDialogBuilder;

import java.util.Locale;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/** Displays the persisted OCR result associated with a confirmed transaction. */
public final class OcrReceiptDetails {
    private static final Pattern ITEM_PATTERN = Pattern.compile(
            "^(.+?)\\s+(\\d+(?:[.,]\\d+)?)\\s*[xX]\\s*([\\d.,]+)\\s+([\\d.,]+)\\s*$");
    private OcrReceiptDetails() { }

    public static void show(Context context, Transaction transaction) {
        show(context, transaction, null);
    }

    public static void show(Context context, Transaction transaction, Runnable onEdit) {
        String receiptId = transaction.getRemoteReceiptId();
        if (receiptId == null || receiptId.trim().isEmpty()) {
            MaterialAlertDialogBuilder builder = new MaterialAlertDialogBuilder(context)
                    .setTitle("Chi tiet giao dich")
                    .setMessage("Giao dich nay khong co chi tiet OCR.");
            if (onEdit != null) {
                builder.setPositiveButton("Sua", (dialog, which) -> onEdit.run())
                        .setNegativeButton("Dong", null);
            } else {
                builder.setPositiveButton("Dong", null);
            }
            builder.show();
            return;
        }
        new ReceiptRepository(context).get(receiptId, new RemoteCallback<ReceiptDto>() {
            @Override public void onSuccess(ReceiptDto receipt) {
                MaterialAlertDialogBuilder builder = new MaterialAlertDialogBuilder(context)
                        .setTitle("Ket qua xu ly OCR")
                        .setView(createReceiptView(context, receipt))
                        .setPositiveButton("Dong", null);
                if (onEdit != null) {
                    builder.setNeutralButton("Sua giao dich", (dialog, which) -> onEdit.run());
                }
                builder.show();
            }
            @Override public void onError(ApiError error) {
                new MaterialAlertDialogBuilder(context)
                        .setTitle("Chi tiet OCR")
                        .setMessage(error.getMessage())
                        .setPositiveButton("Dong", null)
                        .show();
            }
        });
    }

    private static ViewGroup createReceiptView(Context context, ReceiptDto receipt) {
        int padding = Math.round(20 * context.getResources().getDisplayMetrics().density);
        LinearLayout body = new LinearLayout(context);
        body.setOrientation(LinearLayout.VERTICAL);
        body.setPadding(padding, 0, padding, 0);
        addRow(context, body, "Cua hang", safe(receipt.storeName), true);
        addRow(context, body, "Ngay mua", safe(receipt.receiptDate), false);
        TextView heading = text(context, "Mat hang", true);
        heading.setPadding(0, padding, 0, 8);
        body.addView(heading);
        int count = 0;
        if (receipt.lines != null) for (OcrLineDto line : receipt.lines) {
            if (line == null || line.text == null) continue;
            Matcher match = ITEM_PATTERN.matcher(line.text.trim());
            if (!match.matches()) continue;
            addRow(context, body, match.group(1).trim(), "SL " + match.group(2) + "  x  " + match.group(3) + " = " + match.group(4) + " d", false);
            count++;
        }
        if (count == 0) addRow(context, body, "Mat hang", "OCR chua tach duoc chi tiet mat hang tren hoa don nay.", false);
        TextView total = text(context, "Tong thanh toan: " +
                (receipt.totalAmount == null ? "Chua nhan dien" : receipt.totalAmount.toPlainString() + " d"), true);
        total.setPadding(0, padding, 0, padding);
        body.addView(total);
        ScrollView scroll = new ScrollView(context);
        scroll.addView(body);
        return scroll;
    }

    private static void addRow(Context context, LinearLayout parent, String label, String value, boolean prominent) {
        TextView row = text(context, label + "\n" + value, prominent);
        row.setPadding(0, 8, 0, 8);
        parent.addView(row);
    }

    private static TextView text(Context context, String value, boolean bold) {
        TextView view = new TextView(context);
        view.setText(value);
        view.setTextSize(16);
        if (bold) view.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        return view;
    }

    private static String safe(String value) {
        return value == null || value.trim().isEmpty() ? "Khong co" : value;
    }
}
