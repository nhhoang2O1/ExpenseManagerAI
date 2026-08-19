package com.example.appquanlychitieu.ui.transaction;

import android.app.DatePickerDialog;
import android.content.Intent;
import android.os.Bundle;
import android.view.View;
import android.view.ViewGroup;
import android.widget.GridView;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.widget.NestedScrollView;
import androidx.lifecycle.ViewModelProvider;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.model.Category;
import com.example.appquanlychitieu.data.model.TransactionType;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
import com.example.appquanlychitieu.data.remote.dto.CategoryRequestDto;
import com.example.appquanlychitieu.data.remote.dto.TransactionDto;
import com.example.appquanlychitieu.data.remote.dto.TransactionRequestDto;
import com.example.appquanlychitieu.ui.category.CategoryActivity;
import com.example.appquanlychitieu.ui.common.EdgeToEdgeHelper;
import com.example.appquanlychitieu.ui.common.BudgetAlertDialog;
import com.example.appquanlychitieu.ui.receipt.ReceiptScanActivity;
import com.example.appquanlychitieu.util.DateUtils;
import com.example.appquanlychitieu.util.SessionManager;
import com.google.android.material.appbar.MaterialToolbar;
import com.google.android.material.button.MaterialButtonToggleGroup;
import com.google.android.material.button.MaterialButton;
import com.google.android.material.snackbar.Snackbar;
import com.google.android.material.textfield.TextInputEditText;
import com.google.android.material.textfield.TextInputLayout;

import java.math.BigDecimal;
import java.math.RoundingMode;
import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneId;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Locale;

public class AddEditTransactionActivity extends AppCompatActivity {
    private static final int CATEGORY_COLUMNS = 3;
    private static final int CATEGORY_ITEM_HEIGHT_DP = 76;
    private static final int CATEGORY_VERTICAL_SPACING_DP = 8;
    private static final String STATE_DATE = "state_date";
    private static final String STATE_TYPE = "state_type";
    private static final String STATE_CATEGORY = "state_category";

    public static final String EXTRA_TRANSACTION_TYPE = "transaction_type";
    public static final String EXTRA_PREFILL_AMOUNT = "prefill_amount";
    public static final String EXTRA_PREFILL_NOTE = "prefill_note";
    public static final String EXTRA_PREFILL_DATE = "prefill_date";
    public static final String EXTRA_REMOTE_TRANSACTION_ID = "remote_transaction_id";
    public static final String EXTRA_REMOTE_CATEGORY_ID = "remote_category_id";
    public static final String EXTRA_REMOTE_STORE_NAME = "remote_store_name";
    public static final String EXTRA_VERSION = "version";

    private TextInputEditText etAmount;
    private TextInputEditText etNote;
    private TextInputEditText etDate;
    private TextInputEditText etCustomCategory;
    private TextInputLayout layoutAmount;
    private TextInputLayout layoutDate;
    private TextInputLayout layoutCustomCategory;
    private MaterialButtonToggleGroup toggleType;
    private GridView categoriesView;
    private MaterialButton btnSave;
    private View progressSaving;
    private TextView categoryError;
    private NestedScrollView formScroll;
    private LinearLayout calculatorPad;
    private boolean lastAmountWasCalculated;

    private final Map<Long, CategoryDto> categoryMap = new HashMap<>();
    private final List<Category> displayedCategories = new ArrayList<>();
    private CategoryGridViewAdapter categoryAdapter;
    private TransactionFormViewModel viewModel;
    private TransactionType selectedType = TransactionType.EXPENSE;
    private long selectedDate = System.currentTimeMillis();
    private long selectedCategoryId = -1L;
    private CategoryDto selectedCategory;
    private String remoteTransactionId;
    private String remoteCategoryId;
    private String remoteStoreName;
    private long version = 1L;
    private boolean isSubmitting;
    private final ActivityResultLauncher<Intent> receiptScanLauncher = registerForActivityResult(
            new ActivityResultContracts.StartActivityForResult(), result -> {
                if (result.getResultCode() == RESULT_OK) {
                    setResult(RESULT_OK);
                    finish();
                }
            });
    private final ActivityResultLauncher<Intent> categoryManagerLauncher = registerForActivityResult(
            new ActivityResultContracts.StartActivityForResult(), result -> loadCategories());

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_add_edit_transaction);
        EdgeToEdgeHelper.applySystemBarsAndIme(findViewById(R.id.root_transaction_form));

        SessionManager session = new SessionManager(this);
        if (!session.hasAuthToken()) {
            finish();
            return;
        }
        viewModel = new ViewModelProvider(this).get(TransactionFormViewModel.class);
        remoteTransactionId = getIntent().getStringExtra(EXTRA_REMOTE_TRANSACTION_ID);
        remoteCategoryId = getIntent().getStringExtra(EXTRA_REMOTE_CATEGORY_ID);
        remoteStoreName = getIntent().getStringExtra(EXTRA_REMOTE_STORE_NAME);
        version = getIntent().getLongExtra(EXTRA_VERSION, 1L);

        bindViews();
        restoreSelection(savedInstanceState);
        applyPrefill(savedInstanceState == null);
        setupActions();
        loadCategories();
    }

    private void bindViews() {
        MaterialToolbar toolbar = findViewById(R.id.toolbar);
        toolbar.setNavigationOnClickListener(v -> finish());
        toolbar.setTitle(remoteTransactionId == null
                ? R.string.add_transaction : R.string.edit_transaction);
        etAmount = findViewById(R.id.et_amount);
        etNote = findViewById(R.id.et_note);
        etDate = findViewById(R.id.et_date);
        etCustomCategory = findViewById(R.id.et_custom_category);
        layoutAmount = findViewById(R.id.layout_amount);
        layoutDate = findViewById(R.id.layout_date);
        layoutCustomCategory = findViewById(R.id.layout_custom_category);
        toggleType = findViewById(R.id.toggle_type);
        categoriesView = findViewById(R.id.rv_categories);
        btnSave = findViewById(R.id.btn_save);
        progressSaving = findViewById(R.id.progress_saving);
        categoryError = findViewById(R.id.tv_category_error);
        formScroll = findViewById(R.id.scroll_transaction_form);
        calculatorPad = findViewById(R.id.calculator_pad);
        MaterialButton btnScanReceipt = findViewById(R.id.btn_scan_receipt);
        btnScanReceipt.setVisibility(remoteTransactionId == null ? View.VISIBLE : View.GONE);
        btnScanReceipt.setOnClickListener(view -> receiptScanLauncher.launch(
                new Intent(this, ReceiptScanActivity.class)));
        findViewById(R.id.tv_manage_categories).setOnClickListener(view ->
                categoryManagerLauncher.launch(new Intent(this, CategoryActivity.class)));

        etAmount.setInputType(android.text.InputType.TYPE_CLASS_TEXT);
        etAmount.setShowSoftInputOnFocus(false);
        buildCalculatorPad();
        etCustomCategory.setOnFocusChangeListener((view, hasFocus) -> {
            if (hasFocus) {
                formScroll.postDelayed(() -> formScroll.smoothScrollTo(
                        0, layoutCustomCategory.getBottom()), 250L);
            }
        });
        etDate.setText(DateUtils.formatDate(selectedDate));
        categoryAdapter = new CategoryGridViewAdapter(this);
        categoriesView.setAdapter(categoryAdapter);
    }

    private void restoreSelection(Bundle state) {
        String requested = state == null
                ? getIntent().getStringExtra(EXTRA_TRANSACTION_TYPE)
                : state.getString(STATE_TYPE);
        if (requested != null) {
            try { selectedType = TransactionType.valueOf(requested); }
            catch (IllegalArgumentException ignored) { selectedType = TransactionType.EXPENSE; }
        }
        if (state != null) {
            selectedDate = state.getLong(STATE_DATE, selectedDate);
            remoteCategoryId = state.getString(STATE_CATEGORY, remoteCategoryId);
        }
        toggleType.check(selectedType == TransactionType.INCOME ? R.id.btn_income : R.id.btn_expense);
        etDate.setText(DateUtils.formatDate(selectedDate));
    }

    private void applyPrefill(boolean firstCreation) {
        if (!firstCreation) return;
        String amount = getIntent().getStringExtra(EXTRA_PREFILL_AMOUNT);
        String note = getIntent().getStringExtra(EXTRA_PREFILL_NOTE);
        String isoDate = getIntent().getStringExtra(EXTRA_PREFILL_DATE);
        if (amount != null) etAmount.setText(formatExpression(amount));
        if (note != null) etNote.setText(note);
        if (isoDate != null && !isoDate.trim().isEmpty()) {
            try {
                selectedDate = LocalDate.parse(isoDate).atStartOfDay(ZoneId.of("Asia/Ho_Chi_Minh"))
                        .toInstant().toEpochMilli();
                etDate.setText(DateUtils.formatDate(selectedDate));
            } catch (RuntimeException ignored) {
                selectedDate = System.currentTimeMillis();
            }
        }
    }

    private void setupActions() {
        categoriesView.setOnItemClickListener((parent, view, position, id) -> {
            Category category = categoryAdapter.getItem(position);
            selectedCategoryId = category.getId();
            selectedCategory = categoryMap.get(selectedCategoryId);
            remoteCategoryId = selectedCategory == null ? null : selectedCategory.id;
            categoryAdapter.setSelectedPosition(position);
            categoryError.setVisibility(View.GONE);
            updateCustomCategoryVisibility();
        });
        toggleType.addOnButtonCheckedListener((group, checkedId, checked) -> {
            if (!checked) return;
            TransactionType next = checkedId == R.id.btn_income
                    ? TransactionType.INCOME : TransactionType.EXPENSE;
            if (next == selectedType && !displayedCategories.isEmpty()) return;
            selectedType = next;
            selectedCategoryId = -1L;
            selectedCategory = null;
            remoteCategoryId = null;
            etCustomCategory.setText("");
            updateCustomCategoryVisibility();
            loadCategories();
        });
        etDate.setOnClickListener(v -> showDatePicker());
        btnSave.setOnClickListener(v -> saveTransaction());
    }

    private void loadCategories() {
        setSubmitting(true, false);
        viewModel.loadCategories(selectedType.name(), new RemoteCallback<List<CategoryDto>>() {
            @Override
            public void onSuccess(List<CategoryDto> values) {
                displayedCategories.clear();
                categoryMap.clear();
                selectedCategoryId = -1L;
                selectedCategory = null;
                int index = 1;
                for (CategoryDto dto : CategoryDisplayOrder.orderedCopy(values, selectedType)) {
                        long localId = -index++;
                        Category category = new Category(dto.name,
                                empty(dto.icon) ? "ic_other" : dto.icon,
                                empty(dto.color) ? "#607D8B" : dto.color,
                                selectedType, true);
                        category.setId(localId);
                        displayedCategories.add(category);
                        categoryMap.put(localId, dto);
                        if (dto.id != null && dto.id.equals(remoteCategoryId)) {
                            selectedCategoryId = localId;
                            selectedCategory = dto;
                        }
                }
                categoryAdapter.setCategories(displayedCategories);
                updateCategoryGridHeight(displayedCategories.size());
                if (selectedCategoryId != -1L) categoryAdapter.setSelectedCategoryId(selectedCategoryId);
                updateCustomCategoryVisibility();
                setSubmitting(false, false);
                btnSave.setEnabled(!displayedCategories.isEmpty());
            }

            @Override
            public void onError(ApiError error) {
                setSubmitting(false, false);
                btnSave.setEnabled(false);
                categoryError.setText(R.string.category_load_failed);
                categoryError.setVisibility(View.VISIBLE);
                Snackbar.make(findViewById(R.id.root_transaction_form), error.getMessage(),
                        Snackbar.LENGTH_INDEFINITE)
                        .setAction(R.string.retry, v -> loadCategories()).show();
            }
        });
    }

    private void showDatePicker() {
        Calendar calendar = Calendar.getInstance();
        calendar.setTimeInMillis(selectedDate);
        new DatePickerDialog(this, (view, year, month, day) -> {
            Calendar selected = Calendar.getInstance();
            selected.set(year, month, day, 12, 0, 0);
            selectedDate = selected.getTimeInMillis();
            etDate.setText(DateUtils.formatDate(selectedDate));
            layoutDate.setError(null);
        }, calendar.get(Calendar.YEAR), calendar.get(Calendar.MONTH),
                calendar.get(Calendar.DAY_OF_MONTH)).show();
    }

    private void saveTransaction() {
        if (isSubmitting) return;
        long amount;
        try { amount = evaluateAmount(textOf(etAmount)); }
        catch (RuntimeException exception) {
            layoutAmount.setError(getString(R.string.please_enter_amount));
            etAmount.requestFocus();
            return;
        }
        if (amount <= 0) {
            layoutAmount.setError(getString(R.string.amount_must_be_positive));
            etAmount.requestFocus();
            return;
        }
        layoutAmount.setError(null);
        if (selectedCategory == null || selectedCategory.id == null) {
            categoryError.setText(R.string.please_select_category);
            categoryError.setVisibility(View.VISIBLE);
            return;
        }

        String customCategoryName = textOf(etCustomCategory);
        if (CategoryDisplayOrder.isOther(selectedCategory)) {
            if (customCategoryName.isEmpty()) {
                layoutCustomCategory.setError(getString(R.string.custom_category_required));
                etCustomCategory.requestFocus();
                return;
            }
            if (findCategoryByName(customCategoryName) != null) {
                layoutCustomCategory.setError(getString(R.string.category_already_exists_select));
                etCustomCategory.requestFocus();
                return;
            }
        }
        layoutCustomCategory.setError(null);

        String transactionDate = Instant.ofEpochMilli(selectedDate)
                .atZone(ZoneId.of("Asia/Ho_Chi_Minh")).toLocalDate().toString();
        String note = textOf(etNote);
        setSubmitting(true, true);
        if (CategoryDisplayOrder.isOther(selectedCategory)) {
            CategoryRequestDto categoryRequest = new CategoryRequestDto(
                    customCategoryName,
                    selectedType.name(),
                    empty(selectedCategory.color) ? "#607D8B" : selectedCategory.color,
                    "ic_other");
            viewModel.createCategory(categoryRequest, new RemoteCallback<CategoryDto>() {
                @Override
                public void onSuccess(CategoryDto value) {
                    selectedCategory = value;
                    remoteCategoryId = value == null ? null : value.id;
                    saveTransactionWithCategory(value, amount, transactionDate, note);
                }

                @Override
                public void onError(ApiError error) {
                    setSubmitting(false, true);
                    layoutCustomCategory.setError(error.getMessage());
                }
            });
            return;
        }
        saveTransactionWithCategory(selectedCategory, amount, transactionDate, note);
    }

    private void saveTransactionWithCategory(CategoryDto category, long amount,
                                             String transactionDate, String note) {
        if (category == null || category.id == null) {
            setSubmitting(false, true);
            categoryError.setText(R.string.category_load_failed);
            categoryError.setVisibility(View.VISIBLE);
            return;
        }
        TransactionRequestDto request = new TransactionRequestDto(
                BigDecimal.valueOf(amount), selectedType.name(), transactionDate,
                category.id, note, remoteStoreName);
        RemoteCallback<TransactionDto> callback = new RemoteCallback<TransactionDto>() {
            @Override
            public void onSuccess(TransactionDto value) {
                setSubmitting(false, true);
                setResult(RESULT_OK);
                if (BudgetAlertDialog.showIfPresent(
                        AddEditTransactionActivity.this,
                        value == null ? null : value.budgetAlert,
                        AddEditTransactionActivity.this::finish)) return;
                finish();
            }

            @Override
            public void onError(ApiError error) {
                setSubmitting(false, true);
                Snackbar.make(findViewById(R.id.root_transaction_form), error.getMessage(),
                        Snackbar.LENGTH_LONG).show();
            }
        };
        viewModel.save(remoteTransactionId, version, request, callback);
    }

    private void updateCustomCategoryVisibility() {
        boolean visible = CategoryDisplayOrder.isOther(selectedCategory);
        layoutCustomCategory.setVisibility(visible ? View.VISIBLE : View.GONE);
        if (!visible) layoutCustomCategory.setError(null);
    }

    private CategoryDto findCategoryByName(String name) {
        String expected = name == null ? "" : name.trim();
        for (CategoryDto category : categoryMap.values()) {
            if (!CategoryDisplayOrder.isOther(category)
                    && category.name != null
                    && category.name.trim().equalsIgnoreCase(expected)) {
                return category;
            }
        }
        return null;
    }

    private void setSubmitting(boolean submitting, boolean showProgress) {
        isSubmitting = submitting;
        btnSave.setEnabled(!submitting);
        toggleType.setEnabled(!submitting);
        progressSaving.setVisibility(submitting && showProgress ? View.VISIBLE : View.GONE);
    }

    private void updateCategoryGridHeight(int itemCount) {
        int rows = Math.max(1, (int) Math.ceil(itemCount / (double) CATEGORY_COLUMNS));
        ViewGroup.LayoutParams params = categoriesView.getLayoutParams();
        params.height = dp(rows * CATEGORY_ITEM_HEIGHT_DP
                + Math.max(0, rows - 1) * CATEGORY_VERTICAL_SPACING_DP);
        categoriesView.setLayoutParams(params);
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }

    private String textOf(TextInputEditText input) {
        return input.getText() == null ? "" : input.getText().toString().trim();
    }

    private void buildCalculatorPad() {
        String[][] rows = {{"7", "8", "9", "÷"}, {"4", "5", "6", "×"},
                {"1", "2", "3", "−"}, {"C", "0", "⌫", "+"}, {"="}};
        for (String[] row : rows) {
            LinearLayout line = new LinearLayout(this);
            line.setOrientation(LinearLayout.HORIZONTAL);
            for (String label : row) {
                MaterialButton key = new MaterialButton(this, null,
                        com.google.android.material.R.attr.materialButtonOutlinedStyle);
                key.setText(label);
                key.setTextSize(17f);
                key.setMinHeight(dp(44));
                key.setPadding(0, 0, 0, 0);
                LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(0, dp(44), 1f);
                params.setMargins(dp(2), dp(2), dp(2), dp(2));
                line.addView(key, params);
                key.setOnClickListener(v -> onCalculatorKey(label));
            }
            calculatorPad.addView(line, new LinearLayout.LayoutParams(-1, dp(48)));
        }
    }

    private void onCalculatorKey(String key) {
        String current = textOf(etAmount);
        if ("C".equals(key)) { etAmount.setText(""); lastAmountWasCalculated = false; return; }
        if ("⌫".equals(key)) {
            if (!current.isEmpty()) etAmount.setText(current.substring(0, current.length() - 1));
            lastAmountWasCalculated = false;
            return;
        }
        if ("=".equals(key)) {
            try {
                etAmount.setText(String.valueOf(evaluateAmount(current)));
                etAmount.setSelection(etAmount.length());
                lastAmountWasCalculated = true;
                layoutAmount.setError(null);
            } catch (RuntimeException ignored) {
                layoutAmount.setError(getString(R.string.please_enter_amount));
            }
            return;
        }
        if (lastAmountWasCalculated && (Character.isDigit(key.charAt(0)) || ".".equals(key))) {
            current = "";
            lastAmountWasCalculated = false;
        }
        String next = current + key.replace('÷', '/').replace('×', '*').replace('−', '-');
        etAmount.setText(formatExpression(next));
        etAmount.setSelection(etAmount.length());
    }

    private long evaluateAmount(String expression) {
        String input = expression.replace(".", "").replace(",", "").replace(" ", "");
        if (input.isEmpty()) throw new IllegalArgumentException();
        java.util.ArrayDeque<BigDecimal> values = new java.util.ArrayDeque<>();
        java.util.ArrayDeque<Character> operators = new java.util.ArrayDeque<>();
        int index = 0;
        boolean expectNumber = true;
        while (index < input.length()) {
            int start = index;
            if (expectNumber && (input.charAt(index) == '+' || input.charAt(index) == '-')) index++;
            while (index < input.length() && (Character.isDigit(input.charAt(index)) || input.charAt(index) == '.')) index++;
            if (start == index || (expectNumber && index == start + 1 && !Character.isDigit(input.charAt(start))))
                throw new IllegalArgumentException();
            values.push(new BigDecimal(input.substring(start, index)));
            expectNumber = false;
            if (index == input.length()) break;
            char operator = input.charAt(index++);
            if (operator != '+' && operator != '-' && operator != '*' && operator != '/') throw new IllegalArgumentException();
            while (!operators.isEmpty() && precedence(operators.peek()) >= precedence(operator)) applyOperator(values, operators.pop());
            operators.push(operator);
            expectNumber = true;
        }
        if (expectNumber) throw new IllegalArgumentException();
        while (!operators.isEmpty()) applyOperator(values, operators.pop());
        return values.pop().longValueExact();
    }

    private String formatExpression(String expression) {
        StringBuilder result = new StringBuilder();
        StringBuilder number = new StringBuilder();
        for (int i = 0; i < expression.length(); i++) {
            char c = expression.charAt(i);
            if (c == '.') continue;
            if (Character.isDigit(c)) {
                number.append(c);
            } else {
                appendGroupedNumber(result, number);
                number.setLength(0);
                result.append(c);
            }
        }
        appendGroupedNumber(result, number);
        return result.toString();
    }

    private void appendGroupedNumber(StringBuilder result, StringBuilder number) {
        if (number.length() == 0) return;
        try {
            result.append(String.format(Locale.ROOT, "%,d", Long.parseLong(number.toString()))
                    .replace(',', '.'));
        } catch (NumberFormatException ignored) {
            result.append(number);
        }
    }

    private int precedence(char operator) { return operator == '*' || operator == '/' ? 2 : 1; }

    private void applyOperator(java.util.ArrayDeque<BigDecimal> values, char operator) {
        BigDecimal right = values.pop(), left = values.pop();
        BigDecimal result;
        if (operator == '+') result = left.add(right);
        else if (operator == '-') result = left.subtract(right);
        else if (operator == '*') result = left.multiply(right);
        else result = left.divide(right, 10, RoundingMode.HALF_UP).stripTrailingZeros();
        values.push(result);
    }

    private boolean empty(String value) {
        return value == null || value.trim().isEmpty();
    }

    @Override
    protected void onSaveInstanceState(@NonNull Bundle outState) {
        super.onSaveInstanceState(outState);
        outState.putLong(STATE_DATE, selectedDate);
        outState.putString(STATE_TYPE, selectedType.name());
        outState.putString(STATE_CATEGORY, remoteCategoryId);
    }
}
