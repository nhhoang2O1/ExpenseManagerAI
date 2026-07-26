package com.example.appquanlychitieu.ui.transaction;

import android.content.Intent;
import android.os.Bundle;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

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
import com.example.appquanlychitieu.ui.receipt.ReceiptScanActivity;
import com.google.android.material.chip.Chip;
import com.google.android.material.datepicker.MaterialDatePicker;
import com.google.android.material.dialog.MaterialAlertDialogBuilder;
import com.google.android.material.floatingactionbutton.FloatingActionButton;
import com.google.android.material.snackbar.Snackbar;
import com.google.android.material.textfield.TextInputEditText;

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
        TextInputEditText search = view.findViewById(R.id.et_search_transactions);

        adapter = new TransactionListAdapter(requireContext());
        transactionsView.setLayoutManager(new LinearLayoutManager(requireContext()));
        transactionsView.setAdapter(adapter);
        viewModel = new ViewModelProvider(this).get(TransactionListViewModel.class);

        view.findViewById(R.id.btn_scan_receipt).setOnClickListener(v ->
                startActivity(new Intent(requireContext(), ReceiptScanActivity.class)));
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
        search.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int start, int count, int after) {}
            @Override public void afterTextChanged(Editable s) {}
            @Override public void onTextChanged(CharSequence s, int start, int before, int count) {
                viewModel.setSearchQuery(s == null ? "" : s.toString());
            }
        });

        adapter.setOnItemClickListener(new TransactionListAdapter.OnItemClickListener() {
            @Override public void onClick(Transaction transaction) { openTransaction(transaction); }
            @Override public void onLongClick(Transaction transaction) { confirmDelete(transaction); }
        });

        viewModel.getTransactions().observe(getViewLifecycleOwner(), adapter::setTransactions);
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
}
