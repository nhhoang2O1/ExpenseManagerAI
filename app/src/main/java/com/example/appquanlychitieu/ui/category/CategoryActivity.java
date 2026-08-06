package com.example.appquanlychitieu.ui.category;

import android.os.Bundle;
import android.graphics.drawable.Drawable;
import android.text.InputFilter;
import android.view.ViewGroup;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;
import androidx.lifecycle.ViewModelProvider;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.remote.dto.CategoryDto;
import com.example.appquanlychitieu.ui.common.EdgeToEdgeHelper;
import com.example.appquanlychitieu.ui.common.CategoryVisualResolver;
import com.google.android.material.appbar.MaterialToolbar;

import java.util.ArrayList;

public final class CategoryActivity extends AppCompatActivity {
    private CategoryViewModel viewModel;
    private ArrayAdapter<CategoryDto> adapter;

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        int padding = Math.round(16 * getResources().getDisplayMetrics().density);
        root.setPadding(padding, padding, padding, padding);
        MaterialToolbar toolbar = new MaterialToolbar(this);
        toolbar.setTitle(R.string.manage_categories);
        toolbar.setNavigationIcon(R.drawable.ic_back);
        toolbar.setNavigationContentDescription(R.string.content_description_back);
        toolbar.setNavigationOnClickListener(v -> getOnBackPressedDispatcher().onBackPressed());
        Button add = new Button(this);
        add.setText(R.string.add_category);
        ListView list = new ListView(this);
        root.addView(toolbar);
        root.addView(add);
        root.addView(list, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 0, 1));
        setContentView(root);
        EdgeToEdgeHelper.applySystemBars(root);

        adapter = new ArrayAdapter<>(this, android.R.layout.simple_list_item_2,
                android.R.id.text1, new ArrayList<>()) {
            @Override public android.view.View getView(int position, android.view.View convertView,
                                                       ViewGroup parent) {
                android.view.View row = super.getView(position, convertView, parent);
                CategoryDto item = getItem(position);
                TextView name = row.findViewById(android.R.id.text1);
                name.setText(item.name);
                int iconSize = Math.round(24 * getResources().getDisplayMetrics().density);
                Drawable icon = CategoryVisualResolver.resolveIconDrawable(
                        CategoryActivity.this, item.icon, iconSize);
                name.setCompoundDrawablesRelative(icon, null, null, null);
                name.setCompoundDrawablePadding(Math.round(
                        12 * getResources().getDisplayMetrics().density));
                ((TextView) row.findViewById(android.R.id.text2)).setText(
                        getString("INCOME".equals(item.type)
                                ? R.string.type_income : R.string.type_expense));
                return row;
            }
        };
        list.setAdapter(adapter);
        viewModel = new ViewModelProvider(this).get(CategoryViewModel.class);
        viewModel.getCategories().observe(this, items -> {
            adapter.clear();
            if (items != null) adapter.addAll(items);
            adapter.notifyDataSetChanged();
        });
        viewModel.getError().observe(this, message -> {
            if (message != null && !message.trim().isEmpty())
                Toast.makeText(this, message, Toast.LENGTH_LONG).show();
        });
        add.setOnClickListener(v -> showEditor(null));
        list.setOnItemClickListener((p, v, position, id) -> showEditor(adapter.getItem(position)));
        list.setOnItemLongClickListener((p, v, position, id) -> {
            CategoryDto category = adapter.getItem(position);
            new AlertDialog.Builder(this).setTitle(R.string.delete)
                    .setMessage(R.string.confirm_delete)
                    .setPositiveButton(R.string.delete, (d, w) -> viewModel.delete(category))
                    .setNegativeButton(R.string.cancel, null).show();
            return true;
        });
    }

    private void showEditor(CategoryDto category) {
        EditText name = new EditText(this);
        name.setHint(R.string.category_name_hint);
        Spinner type = new Spinner(this);
        String[] types = {getString(R.string.type_expense), getString(R.string.type_income)};
        type.setAdapter(new ArrayAdapter<>(this, android.R.layout.simple_spinner_dropdown_item, types));
        EditText emoji = new EditText(this);
        emoji.setHint(R.string.category_emoji_hint);
        emoji.setSingleLine(true);
        emoji.setFilters(new InputFilter[]{new InputFilter.LengthFilter(16)});
        if (category != null) {
            name.setText(category.name);
            type.setSelection("INCOME".equals(category.type) ? 1 : 0);
            emoji.setText(CategoryVisualResolver.extractEmoji(category.icon));
        }
        LinearLayout form = new LinearLayout(this);
        form.setOrientation(LinearLayout.VERTICAL);
        int padding = Math.round(24 * getResources().getDisplayMetrics().density);
        form.setPadding(padding, 0, padding, 0);
        form.addView(name);
        form.addView(type);
        form.addView(emoji);
        AlertDialog dialog = new AlertDialog.Builder(this).setTitle(category == null
                        ? R.string.add_category : R.string.edit_category)
                .setView(form).setPositiveButton(R.string.save, null)
                .setNegativeButton(R.string.cancel, null).create();
        dialog.setOnShowListener(ignored -> {
            android.widget.Button save = dialog.getButton(AlertDialog.BUTTON_POSITIVE);
            Runnable updateButtonState = () -> save.setEnabled(
                    name.getText() != null && !name.getText().toString().trim().isEmpty()
                            && (category != null || (emoji.getText() != null
                            && CategoryVisualResolver.isEmoji(emoji.getText().toString()))));
            updateButtonState.run();
            name.addTextChangedListener(new android.text.TextWatcher() {
                @Override public void beforeTextChanged(CharSequence value, int start, int count, int after) { }

                @Override public void onTextChanged(CharSequence value, int start, int before, int count) {
                    name.setError(null);
                    updateButtonState.run();
                }

                @Override public void afterTextChanged(android.text.Editable value) { }
            });
            emoji.addTextChangedListener(new android.text.TextWatcher() {
                @Override public void beforeTextChanged(CharSequence value, int start, int count, int after) { }

                @Override public void onTextChanged(CharSequence value, int start, int before, int count) {
                    emoji.setError(null);
                    updateButtonState.run();
                }

                @Override public void afterTextChanged(android.text.Editable value) { }
            });
            save.setOnClickListener(view -> {
                String value = name.getText() == null ? "" : name.getText().toString().trim();
                if (value.isEmpty()) {
                    name.setError(getString(R.string.invalid_name));
                    return;
                }
                String emojiValue = emoji.getText() == null ? "" : emoji.getText().toString().trim();
                if (category == null && !CategoryVisualResolver.isEmoji(emojiValue)) {
                    emoji.setError(getString(R.string.invalid_category_emoji));
                    return;
                }
                String storedIcon = emojiValue.isEmpty() && category != null
                        ? category.icon : CategoryVisualResolver.toEmojiIcon(emojiValue);
                viewModel.save(category, value, type.getSelectedItemPosition() == 1
                                ? "INCOME" : "EXPENSE",
                        storedIcon);
                dialog.dismiss();
            });
        });
        dialog.show();
    }
}
