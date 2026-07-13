package com.example.appquanlychitieu.ui.receipt;

import android.app.DatePickerDialog;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.view.View;
import android.widget.ArrayAdapter;
import android.widget.ImageButton;
import android.widget.ImageView;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.Toast;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.content.FileProvider;
import androidx.lifecycle.ViewModelProvider;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
import com.example.appquanlychitieu.data.remote.dto.ConfirmReceiptRequestDto;
import com.example.appquanlychitieu.data.remote.dto.ReceiptDto;
import com.example.appquanlychitieu.ui.transaction.AddEditTransactionActivity;
import com.example.appquanlychitieu.util.SessionManager;
import com.example.appquanlychitieu.ui.common.EdgeToEdgeHelper;
import com.google.android.material.button.MaterialButton;
import com.google.android.material.appbar.MaterialToolbar;
import com.google.android.material.textfield.MaterialAutoCompleteTextView;
import com.google.android.material.textfield.TextInputEditText;
import com.google.android.material.textfield.TextInputLayout;

import java.io.File;
import java.io.IOException;
import java.time.LocalDate;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

public class ReceiptScanActivity extends AppCompatActivity {
    private static final String STATE_IMAGE_URI = "receipt_image_uri";
    private static final String STATE_STORE = "receipt_draft_store";
    private static final String STATE_DATE = "receipt_draft_date";
    private static final String STATE_TOTAL = "receipt_draft_total";
    private static final String STATE_VAT = "receipt_draft_vat";
    private static final String STATE_NOTE = "receipt_draft_note";
    private static final String STATE_CATEGORY = "receipt_draft_category";

    private ReceiptViewModel viewModel;
    private ImageView ivReceipt;
    private View layoutPicker;
    private View layoutReview;
    private ProgressBar progressOcr;
    private TextView tvStatus;
    private TextView tvError;
    private TextView tvWarning;
    private TextView tvConfidence;
    private TextView tvRawText;
    private TextInputEditText etStoreName;
    private TextInputEditText etReceiptDate;
    private TextInputEditText etTotalAmount;
    private TextInputEditText etVatAmount;
    private TextInputEditText etNote;
    private MaterialAutoCompleteTextView categoryDropdown;
    private TextInputLayout layoutStore;
    private TextInputLayout layoutDate;
    private TextInputLayout layoutTotal;
    private TextInputLayout layoutVat;
    private TextInputLayout layoutCategory;
    private View layoutConfirmBar;
    private TextView stepPick;
    private TextView stepProcess;
    private TextView stepConfirm;
    private MaterialButton btnStartOcr;
    private MaterialButton btnConfirm;
    private MaterialButton btnRetry;
    private List<CategoryDto> categories = new ArrayList<>();
    private Uri selectedImageUri;
    private Uri cameraOutputUri;
    private String populatedReceiptId;
    private CategoryDto selectedCategory;
    private Bundle restoredDraft;
    private boolean draftApplied;

    private final ActivityResultLauncher<String> galleryLauncher =
            registerForActivityResult(new ActivityResultContracts.GetContent(), uri -> {
                if (uri != null) {
                    selectImage(uri);
                }
            });

    private final ActivityResultLauncher<Uri> cameraLauncher =
            registerForActivityResult(new ActivityResultContracts.TakePicture(), success -> {
                if (success && cameraOutputUri != null) {
                    selectImage(cameraOutputUri);
                }
            });

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_receipt_scan);
        EdgeToEdgeHelper.applySystemBars(findViewById(R.id.root_receipt_scan));

        if (!new SessionManager(this).hasAuthToken()) {
            Toast.makeText(this, R.string.backend_login_required, Toast.LENGTH_LONG).show();
            finish();
            return;
        }

        bindViews();
        viewModel = new ViewModelProvider(this).get(ReceiptViewModel.class);
        setupActions();

        if (savedInstanceState != null) {
            restoredDraft = new Bundle(savedInstanceState);
            String savedUri = savedInstanceState.getString(STATE_IMAGE_URI);
            if (savedUri != null) {
                selectImage(Uri.parse(savedUri));
            }
        }

        viewModel.getCategories().observe(this, this::showCategories);
        viewModel.getState().observe(this, this::render);
    }

    private void bindViews() {
        MaterialToolbar toolbar = findViewById(R.id.toolbar);
        toolbar.setNavigationOnClickListener(view -> finish());
        ivReceipt = findViewById(R.id.iv_receipt);
        layoutPicker = findViewById(R.id.layout_picker);
        layoutReview = findViewById(R.id.layout_review);
        progressOcr = findViewById(R.id.progress_ocr);
        tvStatus = findViewById(R.id.tv_status);
        tvError = findViewById(R.id.tv_error);
        tvWarning = findViewById(R.id.tv_warning);
        tvConfidence = findViewById(R.id.tv_confidence);
        tvRawText = findViewById(R.id.tv_raw_text);
        etStoreName = findViewById(R.id.et_store_name);
        etReceiptDate = findViewById(R.id.et_receipt_date);
        etTotalAmount = findViewById(R.id.et_total_amount);
        etVatAmount = findViewById(R.id.et_vat_amount);
        etNote = findViewById(R.id.et_note);
        categoryDropdown = findViewById(R.id.dropdown_category);
        layoutStore = findViewById(R.id.layout_store_name);
        layoutDate = findViewById(R.id.layout_receipt_date);
        layoutTotal = findViewById(R.id.layout_total_amount);
        layoutVat = findViewById(R.id.layout_vat_amount);
        layoutCategory = findViewById(R.id.layout_receipt_category);
        layoutConfirmBar = findViewById(R.id.layout_confirm_bar);
        stepPick = findViewById(R.id.tv_step_pick);
        stepProcess = findViewById(R.id.tv_step_process);
        stepConfirm = findViewById(R.id.tv_step_confirm);
        btnStartOcr = findViewById(R.id.btn_start_ocr);
        btnConfirm = findViewById(R.id.btn_confirm);
        btnRetry = findViewById(R.id.btn_retry);
    }

    private void setupActions() {
        findViewById(R.id.btn_gallery).setOnClickListener(
                view -> galleryLauncher.launch("image/*"));
        findViewById(R.id.btn_camera).setOnClickListener(view -> launchCamera());
        btnStartOcr.setOnClickListener(view -> {
            if (selectedImageUri != null) {
                viewModel.start(selectedImageUri);
            }
        });
        etReceiptDate.setOnClickListener(view -> showDatePicker());
        btnConfirm.setOnClickListener(view -> confirmReceipt());
        btnRetry.setOnClickListener(view -> viewModel.retry());
        findViewById(R.id.btn_retake).setOnClickListener(view -> {
            selectedImageUri = null;
            populatedReceiptId = null;
            ivReceipt.setImageResource(R.drawable.ic_bill);
            btnStartOcr.setEnabled(false);
            viewModel.reset();
            launchCamera();
        });
        findViewById(R.id.btn_manual).setOnClickListener(view -> openManualEntry());
    }

    private void selectImage(Uri uri) {
        selectedImageUri = uri;
        ivReceipt.setImageURI(uri);
        btnStartOcr.setEnabled(true);
        tvStatus.setText(R.string.receipt_ready);
    }

    private void launchCamera() {
        try {
            File directory = new File(getCacheDir(), "receipt_camera");
            if (!directory.exists() && !directory.mkdirs()) {
                throw new IOException("Cannot create camera cache");
            }
            File image = File.createTempFile("receipt_", ".jpg", directory);
            cameraOutputUri = FileProvider.getUriForFile(
                    this,
                    getPackageName() + ".fileprovider",
                    image);
            cameraLauncher.launch(cameraOutputUri);
        } catch (IOException exception) {
            Toast.makeText(this, R.string.camera_unavailable, Toast.LENGTH_SHORT).show();
        }
    }

    private void render(ReceiptViewModel.UiState state) {
        boolean busy = state.phase == ReceiptViewModel.Phase.UPLOADING
                || state.phase == ReceiptViewModel.Phase.PROCESSING
                || state.phase == ReceiptViewModel.Phase.CONFIRMING;
        progressOcr.setVisibility(busy ? View.VISIBLE : View.GONE);
        layoutPicker.setVisibility(
                state.phase == ReceiptViewModel.Phase.PICK_IMAGE
                        || (state.phase == ReceiptViewModel.Phase.ERROR && state.receipt == null)
                        ? View.VISIBLE : View.GONE);
        boolean showReview = state.receipt != null
                && (state.phase == ReceiptViewModel.Phase.REVIEW
                || state.phase == ReceiptViewModel.Phase.CONFIRMING
                || state.phase == ReceiptViewModel.Phase.ERROR);
        layoutReview.setVisibility(showReview ? View.VISIBLE : View.GONE);
        layoutConfirmBar.setVisibility(showReview ? View.VISIBLE : View.GONE);
        findViewById(R.id.btn_gallery).setEnabled(!busy);
        findViewById(R.id.btn_camera).setEnabled(!busy);
        btnStartOcr.setEnabled(!busy && selectedImageUri != null);
        tvError.setVisibility(state.error == null ? View.GONE : View.VISIBLE);
        tvError.setText(state.error == null ? "" : state.error);

        switch (state.phase) {
            case UPLOADING:
                tvStatus.setText(R.string.uploading_receipt);
                break;
            case PROCESSING:
                tvStatus.setText(R.string.processing_receipt);
                break;
            case REVIEW:
                tvStatus.setText(R.string.review_receipt);
                break;
            case CONFIRMING:
                tvStatus.setText(R.string.confirming_receipt);
                break;
            case CONFIRMED:
                Toast.makeText(this, R.string.receipt_confirmed, Toast.LENGTH_SHORT).show();
                setResult(RESULT_OK);
                finish();
                return;
            case ERROR:
                tvStatus.setText(R.string.receipt_error);
                break;
            default:
                if (selectedImageUri == null) {
                    tvStatus.setText(R.string.choose_receipt_image);
                }
        }
        renderSteps(state.phase);

        if (showReview) {
            populateReview(state.receipt);
            applyRestoredDraft();
            boolean ocrFailed = "OCR_FAILED".equalsIgnoreCase(state.receipt.status);
            boolean reviewRequired =
                    "REVIEW_REQUIRED".equalsIgnoreCase(state.receipt.status);
            String classification = state.receipt.classification == null
                    ? "" : state.receipt.classification;
            boolean retrySuggested = ocrFailed
                    || state.phase == ReceiptViewModel.Phase.ERROR
                    || "LOW_QUALITY".equalsIgnoreCase(classification)
                    || "UNRECOGNIZED".equalsIgnoreCase(classification);
            btnConfirm.setEnabled(!busy && reviewRequired);
            btnRetry.setVisibility(retrySuggested ? View.VISIBLE : View.GONE);
        }
    }

    private void populateReview(ReceiptDto receipt) {
        if (receipt.id != null && receipt.id.equals(populatedReceiptId)) {
            return;
        }
        populatedReceiptId = receipt.id;
        etStoreName.setText(receipt.storeName == null ? "" : receipt.storeName);
        etReceiptDate.setText(
                receipt.receiptDate == null ? LocalDate.now().toString() : receipt.receiptDate);
        etTotalAmount.setText(
                receipt.totalAmount == null ? "" : receipt.totalAmount.toPlainString());
        etVatAmount.setText(
                receipt.vatAmount == null ? "" : receipt.vatAmount.toPlainString());

        StringBuilder warning = new StringBuilder();
        String classification = receipt.classification == null ? "" : receipt.classification;
        if ("GENERIC".equalsIgnoreCase(classification)) {
            warning.append(getString(R.string.warning_generic));
        } else if ("UNRECOGNIZED".equalsIgnoreCase(classification)) {
            warning.append(getString(R.string.warning_unrecognized));
        } else if ("LOW_QUALITY".equalsIgnoreCase(classification)) {
            warning.append(getString(R.string.warning_low_quality));
        }
        for (String item : receipt.safeWarnings()) {
            if (warning.length() > 0) {
                warning.append('\n');
            }
            warning.append(item);
        }
        tvWarning.setText(warning);
        tvWarning.setVisibility(warning.length() == 0 ? View.GONE : View.VISIBLE);

        if (receipt.overallConfidence == null) {
            tvConfidence.setText("");
        } else {
            tvConfidence.setText(getString(
                    R.string.ocr_confidence,
                    String.format(Locale.getDefault(), "%.0f%%", receipt.overallConfidence * 100d)));
        }
        tvRawText.setText(receipt.rawText == null ? "" : receipt.rawText);
        tvRawText.setVisibility(
                receipt.rawText == null || receipt.rawText.trim().isEmpty()
                        ? View.GONE : View.VISIBLE);
    }

    private void showCategories(List<CategoryDto> value) {
        categories = value == null ? new ArrayList<>() : value;
        ArrayAdapter<CategoryDto> adapter = new ArrayAdapter<>(
                this,
                android.R.layout.simple_spinner_item,
                categories);
        adapter.setDropDownViewResource(android.R.layout.simple_dropdown_item_1line);
        categoryDropdown.setAdapter(adapter);
        categoryDropdown.setOnItemClickListener((parent, view, position, id) -> {
            selectedCategory = categories.get(position);
            layoutCategory.setError(null);
        });
        if (!categories.isEmpty() && selectedCategory == null) {
            String restoredId = restoredDraft == null ? null
                    : restoredDraft.getString(STATE_CATEGORY);
            for (CategoryDto item : categories) {
                if (item.id != null && item.id.equals(restoredId)) selectedCategory = item;
            }
            if (selectedCategory == null) selectedCategory = categories.get(0);
            categoryDropdown.setText(selectedCategory.toString(), false);
        }
    }

    private void applyRestoredDraft() {
        if (restoredDraft == null || draftApplied) return;
        draftApplied = true;
        etStoreName.setText(restoredDraft.getString(STATE_STORE, textOf(etStoreName)));
        etReceiptDate.setText(restoredDraft.getString(STATE_DATE, textOf(etReceiptDate)));
        etTotalAmount.setText(restoredDraft.getString(STATE_TOTAL, textOf(etTotalAmount)));
        etVatAmount.setText(restoredDraft.getString(STATE_VAT, textOf(etVatAmount)));
        etNote.setText(restoredDraft.getString(STATE_NOTE, textOf(etNote)));
    }

    private void confirmReceipt() {
        String store = textOf(etStoreName);
        String date = textOf(etReceiptDate);
        String total = textOf(etTotalAmount);
        String vat = textOf(etVatAmount);
        CategoryDto category = selectedCategory;

        ReceiptReviewValidator.ValidationResult result = ReceiptReviewValidator.validate(
                store,
                date,
                total,
                vat,
                category == null ? null : category.id);
        if (!result.valid) {
            showValidationError(result.field);
            return;
        }

        viewModel.confirm(new ConfirmReceiptRequestDto(
                store,
                date,
                result.totalAmount,
                result.vatAmount,
                category.id,
                textOf(etNote)));
    }

    private void showValidationError(ReceiptReviewValidator.Field field) {
        layoutStore.setError(null);
        layoutDate.setError(null);
        layoutTotal.setError(null);
        layoutVat.setError(null);
        layoutCategory.setError(null);
        int message;
        switch (field) {
            case STORE:
                message = R.string.invalid_store_name;
                layoutStore.setError(getString(message));
                etStoreName.requestFocus();
                break;
            case DATE:
                message = R.string.invalid_receipt_date;
                layoutDate.setError(getString(message));
                etReceiptDate.requestFocus();
                break;
            case TOTAL:
                message = R.string.invalid_total_amount;
                layoutTotal.setError(getString(message));
                etTotalAmount.requestFocus();
                break;
            case VAT:
                message = R.string.invalid_vat_amount;
                layoutVat.setError(getString(message));
                etVatAmount.requestFocus();
                break;
            default:
                message = R.string.please_select_category;
                layoutCategory.setError(getString(message));
        }
    }

    private void renderSteps(ReceiptViewModel.Phase phase) {
        int active = phase == ReceiptViewModel.Phase.PICK_IMAGE ? 0
                : (phase == ReceiptViewModel.Phase.UPLOADING
                || phase == ReceiptViewModel.Phase.PROCESSING ? 1 : 2);
        TextView[] steps = {stepPick, stepProcess, stepConfirm};
        for (int i = 0; i < steps.length; i++) {
            steps[i].setTextColor(getColor(i <= active ? R.color.primary : R.color.text_hint));
            steps[i].setTypeface(null, i == active
                    ? android.graphics.Typeface.BOLD : android.graphics.Typeface.NORMAL);
        }
    }

    private void showDatePicker() {
        LocalDate current;
        try {
            current = LocalDate.parse(textOf(etReceiptDate));
        } catch (RuntimeException ignored) {
            current = LocalDate.now();
        }
        new DatePickerDialog(
                this,
                (view, year, month, day) ->
                        etReceiptDate.setText(LocalDate.of(year, month + 1, day).toString()),
                current.getYear(),
                current.getMonthValue() - 1,
                current.getDayOfMonth())
                .show();
    }

    private void openManualEntry() {
        Intent intent = new Intent(this, AddEditTransactionActivity.class);
        intent.putExtra(AddEditTransactionActivity.EXTRA_TRANSACTION_TYPE, "EXPENSE");
        intent.putExtra(AddEditTransactionActivity.EXTRA_PREFILL_AMOUNT, textOf(etTotalAmount));
        intent.putExtra(AddEditTransactionActivity.EXTRA_PREFILL_NOTE, textOf(etStoreName));
        intent.putExtra(AddEditTransactionActivity.EXTRA_PREFILL_DATE, textOf(etReceiptDate));
        startActivity(intent);
    }

    private String textOf(TextInputEditText input) {
        return input.getText() == null ? "" : input.getText().toString().trim();
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        super.onSaveInstanceState(outState);
        if (selectedImageUri != null) {
            outState.putString(STATE_IMAGE_URI, selectedImageUri.toString());
        }
        outState.putString(STATE_STORE, textOf(etStoreName));
        outState.putString(STATE_DATE, textOf(etReceiptDate));
        outState.putString(STATE_TOTAL, textOf(etTotalAmount));
        outState.putString(STATE_VAT, textOf(etVatAmount));
        outState.putString(STATE_NOTE, textOf(etNote));
        if (selectedCategory != null) outState.putString(STATE_CATEGORY, selectedCategory.id);
    }
}
