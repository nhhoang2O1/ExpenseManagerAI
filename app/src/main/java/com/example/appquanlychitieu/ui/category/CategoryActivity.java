package com.example.appquanlychitieu.ui.category;

import android.graphics.drawable.Drawable;
import android.os.Bundle;
import android.text.InputFilter;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ArrayAdapter;
import android.widget.EditText;
import android.widget.ImageButton;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;
import androidx.lifecycle.ViewModelProvider;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.model.TransactionType;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
import com.example.appquanlychitieu.ui.common.CategoryVisualResolver;
import com.example.appquanlychitieu.ui.common.EdgeToEdgeHelper;
import com.example.appquanlychitieu.ui.transaction.CategoryDisplayOrder;
import com.google.android.material.appbar.MaterialToolbar;
import com.google.android.material.button.MaterialButton;
import com.google.android.material.tabs.TabLayout;

import java.util.ArrayList;
import java.util.List;

public final class CategoryActivity extends AppCompatActivity {
    private CategoryViewModel viewModel;
    private ArrayAdapter<CategoryDto> adapter;
    private MaterialButton addButton;
    private TextView emptyState;
    private final List<CategoryDto> allCategories = new ArrayList<>();
    private TransactionType selectedType = TransactionType.EXPENSE;

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        setContentView(R.layout.activity_category);
        EdgeToEdgeHelper.applySystemBars(findViewById(R.id.root_category_management));

        MaterialToolbar toolbar = findViewById(R.id.toolbar);
        toolbar.setNavigationContentDescription(R.string.content_description_back);
        toolbar.setNavigationOnClickListener(v -> getOnBackPressedDispatcher().onBackPressed());

        TabLayout tabs = findViewById(R.id.tabs_category_type);
        tabs.addTab(tabs.newTab().setText(R.string.type_expense), true);
        tabs.addTab(tabs.newTab().setText(R.string.type_income));
        tabs.addOnTabSelectedListener(new TabLayout.OnTabSelectedListener() {
            @Override
            public void onTabSelected(TabLayout.Tab tab) {
                selectedType = tab.getPosition() == 0
                        ? TransactionType.EXPENSE : TransactionType.INCOME;
                renderCategories();
            }

            @Override public void onTabUnselected(TabLayout.Tab tab) { }
            @Override public void onTabReselected(TabLayout.Tab tab) { }
        });

        addButton = findViewById(R.id.btn_add_category);
        emptyState = findViewById(R.id.tv_empty_categories);
        ListView list = findViewById(R.id.list_categories);
        adapter = createAdapter();
        list.setAdapter(adapter);
        list.setEmptyView(emptyState);

        viewModel = new ViewModelProvider(this).get(CategoryViewModel.class);
        viewModel.getCategories().observe(this, items -> {
            allCategories.clear();
            if (items != null) allCategories.addAll(items);
            renderCategories();
        });
        viewModel.getError().observe(this, message -> {
            if (message != null && !message.trim().isEmpty()) {
                Toast.makeText(this, message, Toast.LENGTH_LONG).show();
            }
        });
        addButton.setOnClickListener(v -> showEditor(null));
        renderCategories();
    }

    private ArrayAdapter<CategoryDto> createAdapter() {
        return new ArrayAdapter<CategoryDto>(this, R.layout.item_category_manage,
                R.id.tv_category_name, new ArrayList<>()) {
            @Override
            public View getView(int position, View convertView, ViewGroup parent) {
                View row = convertView;
                RowHolder holder;
                if (row == null) {
                    row = LayoutInflater.from(CategoryActivity.this)
                            .inflate(R.layout.item_category_manage, parent, false);
                    holder = new RowHolder(row);
                    row.setTag(holder);
                } else {
                    holder = (RowHolder) row.getTag();
                }

                CategoryDto item = getItem(position);
                holder.name.setText(item == null ? "" : item.name);
                if (item != null && !item.isActive) {
                    holder.name.setText(item.name + " (Đã ẩn)");
                    holder.name.setAlpha(0.55f);
                } else {
                    holder.name.setAlpha(1f);
                }
                int iconSize = Math.round(24 * getResources().getDisplayMetrics().density);
                Drawable icon = CategoryVisualResolver.resolveIconDrawable(
                        CategoryActivity.this, item == null ? null : item.icon, iconSize);
                holder.icon.setImageDrawable(icon);
                row.setOnClickListener(v -> showEditor(item));
                holder.delete.setOnClickListener(v -> confirmDelete(item));
                holder.toggle.setOnClickListener(v -> viewModel.setActive(item, item == null || !item.isActive));
                return row;
            }
        };
    }

    private void renderCategories() {
        if (adapter == null) return;
        List<CategoryDto> visible = new ArrayList<>();
        for (CategoryDto item : allCategories) {
            if (item != null && selectedType.name().equalsIgnoreCase(item.type)) {
                visible.add(item);
            }
        }
        adapter.clear();
        adapter.addAll(CategoryDisplayOrder.orderedCopy(visible, selectedType));
        adapter.notifyDataSetChanged();
        addButton.setText(selectedType == TransactionType.EXPENSE
                ? R.string.add_expense_category : R.string.add_income_category);
        emptyState.setText(selectedType == TransactionType.EXPENSE
                ? R.string.no_expense_categories : R.string.no_income_categories);
    }

    private void confirmDelete(CategoryDto category) {
        if (category == null) return;
        new AlertDialog.Builder(this)
                .setTitle(R.string.delete_category)
                .setMessage(getString(R.string.confirm_delete_category, category.name))
                .setPositiveButton(R.string.delete, (dialog, which) -> viewModel.delete(category))
                .setNegativeButton(R.string.cancel, null)
                .show();
    }

    private void showEditor(CategoryDto category) {
        EditText name = new EditText(this);
        name.setHint(R.string.category_name_hint);
        Spinner type = new Spinner(this);
        String[] types = {getString(R.string.type_expense), getString(R.string.type_income)};
        type.setAdapter(new ArrayAdapter<>(this,
                android.R.layout.simple_spinner_dropdown_item, types));
        type.setSelection(category == null
                ? (selectedType == TransactionType.INCOME ? 1 : 0)
                : ("INCOME".equals(category.type) ? 1 : 0));
        type.setEnabled(category != null);

        EditText emoji = new EditText(this);
        emoji.setHint(R.string.category_emoji_hint);
        emoji.setSingleLine(true);
        emoji.setFilters(new InputFilter[]{new InputFilter.LengthFilter(16)});
        if (category != null) {
            name.setText(category.name);
            emoji.setText(CategoryVisualResolver.extractEmoji(category.icon));
        }

        LinearLayout form = new LinearLayout(this);
        form.setOrientation(LinearLayout.VERTICAL);
        int padding = Math.round(24 * getResources().getDisplayMetrics().density);
        form.setPadding(padding, 0, padding, 0);
        form.addView(name);
        form.addView(type);
        form.addView(emoji);

        AlertDialog dialog = new AlertDialog.Builder(this)
                .setTitle(category == null ? R.string.add_category : R.string.edit_category)
                .setView(form)
                .setPositiveButton(R.string.save, null)
                .setNegativeButton(R.string.cancel, null)
                .create();
        dialog.setOnShowListener(ignored -> {
            android.widget.Button save = dialog.getButton(AlertDialog.BUTTON_POSITIVE);
            Runnable updateButtonState = () -> save.setEnabled(
                    name.getText() != null && !name.getText().toString().trim().isEmpty()
                            && (category != null || (emoji.getText() != null
                            && CategoryVisualResolver.isEmoji(emoji.getText().toString()))));
            updateButtonState.run();
            name.addTextChangedListener(new SimpleTextWatcher(() -> {
                name.setError(null);
                updateButtonState.run();
            }));
            emoji.addTextChangedListener(new SimpleTextWatcher(() -> {
                emoji.setError(null);
                updateButtonState.run();
            }));
            save.setOnClickListener(view -> {
                String value = textOf(name);
                if (value.isEmpty()) {
                    name.setError(getString(R.string.invalid_name));
                    return;
                }
                String emojiValue = textOf(emoji);
                if (category == null && !CategoryVisualResolver.isEmoji(emojiValue)) {
                    emoji.setError(getString(R.string.invalid_category_emoji));
                    return;
                }
                String storedIcon = emojiValue.isEmpty() && category != null
                        ? category.icon : CategoryVisualResolver.toEmojiIcon(emojiValue);
                viewModel.save(category, value,
                        type.getSelectedItemPosition() == 1 ? "INCOME" : "EXPENSE",
                        storedIcon);
                dialog.dismiss();
            });
        });
        dialog.show();
    }

    private String textOf(EditText input) {
        return input.getText() == null ? "" : input.getText().toString().trim();
    }

    private static final class RowHolder {
        final ImageView icon;
        final TextView name;
        final ImageButton delete;
        final ImageButton toggle;

        RowHolder(View row) {
            icon = row.findViewById(R.id.iv_category_icon);
            name = row.findViewById(R.id.tv_category_name);
            delete = row.findViewById(R.id.btn_delete_category);
            toggle = row.findViewById(R.id.btn_toggle_category);
        }
    }

    private static final class SimpleTextWatcher implements android.text.TextWatcher {
        private final Runnable onChanged;

        SimpleTextWatcher(Runnable onChanged) {
            this.onChanged = onChanged;
        }

        @Override public void beforeTextChanged(CharSequence value, int start, int count, int after) { }
        @Override public void onTextChanged(CharSequence value, int start, int before, int count) {
            onChanged.run();
        }
        @Override public void afterTextChanged(android.text.Editable value) { }
    }
}
