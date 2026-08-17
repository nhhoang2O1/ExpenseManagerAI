package com.example.appquanlychitieu.ui.transaction;

import android.content.Context;
import android.content.Intent;
import android.graphics.Typeface;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ArrayAdapter;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.core.util.Pair;
import androidx.fragment.app.Fragment;
import androidx.lifecycle.ViewModelProvider;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.model.Transaction;
import com.example.appquanlychitieu.data.model.TransactionType;
import com.example.appquanlychitieu.ui.common.LoadState;
import com.google.android.material.chip.Chip;
import com.google.android.material.datepicker.MaterialDatePicker;
import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.google.android.material.floatingactionbutton.FloatingActionButton;
import com.google.android.material.snackbar.Snackbar;
import com.google.android.material.textfield.MaterialAutoCompleteTextView;
import java.util.ArrayList;
import java.util.List;

import java.time.Instant;
import java.time.ZoneId;

public class TransactionListFragment extends Fragment {
    private TransactionListViewModel viewModel;
    private TransactionListAdapter adapter;
    private RecyclerView transactionsView;
    private View emptyState;
    private View errorState;
    private View loading;
    private View syncBanner;
    private Chip chipAll;
    private Chip chipExpense;
    private Chip chipIncome;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container,
                             @Nullable Bundle savedInstanceState) {
        return inflater.inflate(R.layout.fragment_transaction_list, container, false);
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        transactionsView = view.findViewById(R.id.rv_transactions);
        emptyState = view.findViewById(R.id.layout_empty_state);
        errorState = view.findViewById(R.id.layout_error_state);
        loading = view.findViewById(R.id.progress_loading);
        syncBanner = view.findViewById(R.id.layout_sync_banner);
        chipAll = view.findViewById(R.id.chip_all);
        chipExpense = view.findViewById(R.id.chip_expense);
        chipIncome = view.findViewById(R.id.chip_income);
        MaterialAutoCompleteTextView categoryDropdown =
                view.findViewById(R.id.dropdown_transaction_category);
        CategoryDropdownAdapter categoryDropdownAdapter =
                new CategoryDropdownAdapter(requireContext());
        categoryDropdown.setAdapter(categoryDropdownAdapter);

        adapter = new TransactionListAdapter(requireContext());
        transactionsView.setLayoutManager(new LinearLayoutManager(requireContext()));
        transactionsView.setAdapter(adapter);
        viewModel = new ViewModelProvider(this).get(TransactionListViewModel.class);

        view.findViewById(R.id.btn_filter_date).setOnClickListener(v -> showDateRangePicker());
        view.findViewById(R.id.btn_retry).setOnClickListener(v -> viewModel.refreshRemoteTransactions());
        view.findViewById(R.id.btn_banner_retry).setOnClickListener(v -> viewModel.refreshRemoteTransactions());
        view.findViewById(R.id.btn_empty_cta).setOnClickListener(v -> openAddTransaction());

        chipAll.setOnCheckedChangeListener((button, checked) -> {
            if (checked) viewModel.setFilterType("ALL");
        });
        chipExpense.setOnCheckedChangeListener((button, checked) -> {
            if (checked) viewModel.setFilterType("EXPENSE");
        });
        chipIncome.setOnCheckedChangeListener((button, checked) -> {
            if (checked) viewModel.setFilterType("INCOME");
        });
        categoryDropdown.setOnItemClickListener((parent, itemView, position, id) -> {
            CategoryDropdownItem item = categoryDropdownAdapter.getItem(position);
            if (item == null || item.header) return;
            viewModel.setCategoryFilter(item.categoryName, item.type);
        });

        adapter.setOnItemClickListener(new TransactionListAdapter.OnItemClickListener() {
            @Override public void onClick(Transaction transaction) { openTransaction(transaction); }
            @Override public void onLongClick(Transaction transaction) { confirmDelete(transaction); }
        });

        viewModel.getTransactions().observe(getViewLifecycleOwner(), adapter::setTransactions);
        viewModel.getCategoryOptions().observe(getViewLifecycleOwner(), options -> {
            categoryDropdownAdapter.replace(options, chipAll.isChecked());
            String selected = viewModel.getSelectedCategory().getValue();
            categoryDropdown.setText(selected == null || selected.trim().isEmpty()
                    ? getString(R.string.all_categories) : selected, false);
        });
        viewModel.getLoadState().observe(getViewLifecycleOwner(), this::renderState);
        viewModel.getRemoteError().observe(getViewLifecycleOwner(), message -> {
            boolean hasMessage = message != null && !message.trim().isEmpty();
            syncBanner.setVisibility(hasMessage && !adapter.getCurrentList().isEmpty()
                    ? View.VISIBLE : View.GONE);
        });
        viewModel.getFeedback().observe(getViewLifecycleOwner(), message -> {
            if (message != null && !message.trim().isEmpty()) {
                Snackbar.make(view, message, Snackbar.LENGTH_SHORT).show();
            }
        });
    }

    private void renderState(LoadState state) {
        if (state == null) return;
        loading.setVisibility(state == LoadState.LOADING ? View.VISIBLE : View.GONE);
        errorState.setVisibility(state == LoadState.ERROR ? View.VISIBLE : View.GONE);
        emptyState.setVisibility(state == LoadState.EMPTY ? View.VISIBLE : View.GONE);
        transactionsView.setVisibility(
                state == LoadState.CONTENT || (state == LoadState.ERROR && !adapter.getCurrentList().isEmpty())
                        ? View.VISIBLE : View.GONE);
    }

    private void showDateRangePicker() {
        MaterialDatePicker<Pair<Long, Long>> picker = MaterialDatePicker.Builder.dateRangePicker()
                .setTitleText(R.string.date_range_title)
                .build();
        picker.addOnPositiveButtonClickListener(selection -> {
            if (selection.first == null || selection.second == null) return;
            long days = (selection.second - selection.first) / 86_400_000L;
            if (days > 15) {
                Snackbar.make(requireView(), R.string.date_range_limit, Snackbar.LENGTH_LONG).show();
                return;
            }
            viewModel.setDateRange(selection.first, selection.second + 86_399_999L);
        });
        picker.show(getChildFragmentManager(), "transaction_date_range");
    }

    private void openAddTransaction() {
        Intent intent = new Intent(requireContext(), AddEditTransactionActivity.class);
        if (chipIncome.isChecked()) intent.putExtra(
                AddEditTransactionActivity.EXTRA_TRANSACTION_TYPE, TransactionType.INCOME.name());
        else if (chipExpense.isChecked()) intent.putExtra(
                AddEditTransactionActivity.EXTRA_TRANSACTION_TYPE, TransactionType.EXPENSE.name());
        startActivity(intent);
    }

    private void openTransaction(Transaction transaction) {
        if (transaction.isGoalCompletion()) {
            Snackbar.make(requireView(), R.string.goal_transaction_read_only,
                    Snackbar.LENGTH_LONG).show();
            return;
        }
        editTransaction(transaction);
    }

    private void editTransaction(Transaction transaction) {
        Intent intent = new Intent(requireContext(), AddEditTransactionActivity.class);
        intent.putExtra(AddEditTransactionActivity.EXTRA_REMOTE_TRANSACTION_ID, transaction.getRemoteId());
        intent.putExtra(AddEditTransactionActivity.EXTRA_REMOTE_CATEGORY_ID, transaction.getRemoteCategoryId());
        intent.putExtra(AddEditTransactionActivity.EXTRA_REMOTE_STORE_NAME, transaction.getRemoteStoreName());
        intent.putExtra(AddEditTransactionActivity.EXTRA_VERSION, transaction.getVersion());
        intent.putExtra(AddEditTransactionActivity.EXTRA_PREFILL_AMOUNT,
                String.valueOf((long) transaction.getAmount()));
        intent.putExtra(AddEditTransactionActivity.EXTRA_PREFILL_NOTE, transaction.getNote());
        intent.putExtra(AddEditTransactionActivity.EXTRA_PREFILL_DATE,
                Instant.ofEpochMilli(transaction.getDate()).atZone(ZoneId.of("Asia/Ho_Chi_Minh"))
                        .toLocalDate().toString());
        intent.putExtra(AddEditTransactionActivity.EXTRA_TRANSACTION_TYPE, transaction.getType().name());
        startActivity(intent);
    }

    private void confirmDelete(Transaction transaction) {
        if (transaction.isGoalCompletion()) {
            Snackbar.make(requireView(), R.string.goal_transaction_read_only,
                    Snackbar.LENGTH_LONG).show();
            return;
        }
        new MaterialAlertDialogBuilder(requireContext())
                .setTitle(R.string.confirm_delete_title)
                .setMessage(R.string.confirm_delete)
                .setPositiveButton(R.string.delete, (dialog, which) ->
                        viewModel.deleteTransaction(transaction))
                .setNegativeButton(R.string.cancel, null)
                .show();
    }

    @Override
    public void onResume() {
        super.onResume();
        if (viewModel != null) viewModel.refreshRemoteTransactions();
        FloatingActionButton fab = requireActivity().findViewById(R.id.fab_add_transaction);
        if (fab != null) fab.setOnClickListener(v -> openAddTransaction());
    }

    private static final class CategoryDropdownItem {
        final String label;
        final String categoryName;
        final TransactionType type;
        final boolean header;

        private CategoryDropdownItem(
                String label, String categoryName, TransactionType type, boolean header) {
            this.label = label;
            this.categoryName = categoryName;
            this.type = type;
            this.header = header;
        }

        static CategoryDropdownItem all(String label) {
            return new CategoryDropdownItem(label, "", null, false);
        }

        static CategoryDropdownItem header(String label) {
            return new CategoryDropdownItem(label, null, null, true);
        }

        static CategoryDropdownItem category(
                TransactionListViewModel.CategoryFilterOption option) {
            return new CategoryDropdownItem(
                    option.getName(), option.getName(), option.getType(), false);
        }

        @NonNull
        @Override public String toString() { return label; }
    }

    private static final class CategoryDropdownAdapter extends ArrayAdapter<CategoryDropdownItem> {
        CategoryDropdownAdapter(Context context) {
            super(context, android.R.layout.simple_dropdown_item_1line, new ArrayList<>());
        }

        void replace(
                List<TransactionListViewModel.CategoryFilterOption> options,
                boolean grouped) {
            setNotifyOnChange(false);
            clear();
            add(CategoryDropdownItem.all(getContext().getString(R.string.all_categories)));
            if (grouped) {
                addGroup(options, TransactionType.EXPENSE, R.string.category_group_expense);
                addGroup(options, TransactionType.INCOME, R.string.category_group_income);
            } else if (options != null) {
                for (TransactionListViewModel.CategoryFilterOption option : options) {
                    add(CategoryDropdownItem.category(option));
                }
            }
            notifyDataSetChanged();
        }

        private void addGroup(
                List<TransactionListViewModel.CategoryFilterOption> options,
                TransactionType type,
                int titleResource) {
            if (options == null) return;
            boolean hasItems = false;
            for (TransactionListViewModel.CategoryFilterOption option : options) {
                if (option.getType() == type) { hasItems = true; break; }
            }
            if (!hasItems) return;
            add(CategoryDropdownItem.header(getContext().getString(titleResource)));
            for (TransactionListViewModel.CategoryFilterOption option : options) {
                if (option.getType() == type) add(CategoryDropdownItem.category(option));
            }
        }

        @Override public boolean areAllItemsEnabled() { return false; }

        @Override public boolean isEnabled(int position) {
            CategoryDropdownItem item = getItem(position);
            return item != null && !item.header;
        }

        @NonNull
        @Override
        public View getView(int position, @Nullable View convertView, @NonNull ViewGroup parent) {
            TextView row = (TextView) super.getView(position, convertView, parent);
            CategoryDropdownItem item = getItem(position);
            boolean header = item != null && item.header;
            row.setText(item == null ? "" : item.label);
            row.setTextColor(getContext().getColor(
                    header ? R.color.primary : R.color.text_primary));
            row.setTypeface(Typeface.DEFAULT, header ? Typeface.BOLD : Typeface.NORMAL);
            row.setTextSize(header ? 11f : 14f);
            int horizontal = dp(16);
            row.setPadding(horizontal, 0, horizontal, 0);
            row.setMinHeight(dp(header ? 36 : 48));
            return row;
        }

        private int dp(int value) {
            return Math.round(value * getContext().getResources().getDisplayMetrics().density);
        }
    }
}
