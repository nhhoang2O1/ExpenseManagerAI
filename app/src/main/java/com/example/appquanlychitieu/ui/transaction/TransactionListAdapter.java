package com.example.appquanlychitieu.ui.transaction;

import android.content.Context;
import android.graphics.drawable.GradientDrawable;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ImageButton;
import android.widget.ImageView;
import android.widget.PopupMenu;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.DiffUtil;
import androidx.recyclerview.widget.ListAdapter;
import androidx.recyclerview.widget.RecyclerView;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.model.Category;
import com.example.appquanlychitieu.data.model.Transaction;
import com.example.appquanlychitieu.data.model.TransactionType;
import com.example.appquanlychitieu.ui.common.CategoryVisualResolver;
import com.example.appquanlychitieu.util.CurrencyFormatter;
import com.example.appquanlychitieu.util.DateUtils;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.Map;
import java.util.Objects;

public class TransactionListAdapter
        extends ListAdapter<Transaction, TransactionListAdapter.ViewHolder> {
    public interface OnItemClickListener {
        void onClick(Transaction transaction);
        void onLongClick(Transaction transaction);
    }

    private final Context context;
    private final Map<Long, Category> categoryCache = new HashMap<>();
    private OnItemClickListener listener;

    public TransactionListAdapter(Context context) {
        super(DIFF_CALLBACK);
        this.context = context;
    }

    public void setOnItemClickListener(OnItemClickListener listener) {
        this.listener = listener;
    }

    public void setTransactions(java.util.List<Transaction> transactions) {
        submitList(transactions == null ? new ArrayList<>() : new ArrayList<>(transactions));
    }

    public void setCategoryCache(Map<Long, Category> cache) {
        categoryCache.clear();
        if (cache != null) categoryCache.putAll(cache);
        notifyItemRangeChanged(0, getItemCount());
    }

    @NonNull
    @Override
    public ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        return new ViewHolder(LayoutInflater.from(parent.getContext())
                .inflate(R.layout.item_transaction, parent, false));
    }

    @Override
    public void onBindViewHolder(@NonNull ViewHolder holder, int position) {
        Transaction transaction = getItem(position);
        Transaction previous = position > 0 ? getItem(position - 1) : null;
        holder.bind(transaction, previous);
    }

    final class ViewHolder extends RecyclerView.ViewHolder {
        final View viewIconBg;
        final ImageView ivCategoryIcon;
        final TextView tvDateGroup;
        final TextView tvNote;
        final TextView tvCategory;
        final TextView tvAmount;
        final ImageButton btnMore;

        ViewHolder(View view) {
            super(view);
            tvDateGroup = view.findViewById(R.id.tv_date_group);
            viewIconBg = view.findViewById(R.id.view_icon_bg);
            ivCategoryIcon = view.findViewById(R.id.iv_category_icon);
            tvNote = view.findViewById(R.id.tv_note);
            tvCategory = view.findViewById(R.id.tv_category);
            tvAmount = view.findViewById(R.id.tv_amount);
            btnMore = view.findViewById(R.id.btn_more);
        }

        void bind(Transaction transaction, Transaction previous) {
            Category local = categoryCache.get(transaction.getCategoryId());
            String categoryName = local != null ? local.getName() : transaction.getRemoteCategoryName();
            String categoryColor = local != null ? local.getColor() : transaction.getRemoteCategoryColor();
            String categoryIcon = local != null ? local.getIcon() : transaction.getRemoteCategoryIcon();
            String categoryId = transaction.getRemoteCategoryId() == null
                    ? String.valueOf(transaction.getCategoryId()) : transaction.getRemoteCategoryId();

            if (categoryName == null || categoryName.trim().isEmpty()) {
                categoryName = context.getString(R.string.remote_transaction);
            }
            String note = transaction.getNote();
            tvNote.setText(categoryName);
            tvCategory.setText(note == null || note.trim().isEmpty()
                    ? DateUtils.getRelativeDateLabel(transaction.getDate())
                    : note + " · " + DateUtils.getRelativeDateLabel(transaction.getDate()));

            boolean expense = transaction.getType() == TransactionType.EXPENSE;
            tvAmount.setText(CurrencyFormatter.formatWithSign(transaction.getAmount(), expense));
            tvAmount.setTextColor(context.getColor(expense ? R.color.expense_color : R.color.income_color));

            CategoryVisualResolver.CategoryVisual visual =
                    CategoryVisualResolver.resolve(context, categoryId, categoryColor);
            GradientDrawable background = new GradientDrawable();
            background.setShape(GradientDrawable.OVAL);
            background.setColor(visual.baseColor);
            viewIconBg.setBackground(background);
            ivCategoryIcon.setColorFilter(visual.onBaseColor);
            ivCategoryIcon.setImageResource(CategoryVisualResolver.resolveIcon(categoryIcon));

            boolean newDay = previous == null
                    || !DateUtils.formatDate(previous.getDate()).equals(DateUtils.formatDate(transaction.getDate()));
            tvDateGroup.setVisibility(newDay ? View.VISIBLE : View.GONE);
            if (newDay) tvDateGroup.setText(DateUtils.getRelativeDateLabel(transaction.getDate()));

            itemView.setOnClickListener(v -> {
                if (listener != null) listener.onClick(transaction);
            });
            itemView.setOnLongClickListener(v -> {
                if (listener != null) listener.onLongClick(transaction);
                return true;
            });
            btnMore.setOnClickListener(v -> showMenu(v, transaction));
        }

        private void showMenu(View anchor, Transaction transaction) {
            PopupMenu menu = new PopupMenu(context, anchor);
            menu.getMenu().add(context.getString(R.string.edit));
            menu.getMenu().add(context.getString(R.string.delete));
            menu.setOnMenuItemClickListener(item -> {
                if (listener == null) return false;
                if (item.getTitle().toString().equals(context.getString(R.string.delete))) {
                    listener.onLongClick(transaction);
                } else {
                    listener.onClick(transaction);
                }
                return true;
            });
            menu.show();
        }

    }

    private static final DiffUtil.ItemCallback<Transaction> DIFF_CALLBACK =
            new DiffUtil.ItemCallback<Transaction>() {
                @Override
                public boolean areItemsTheSame(@NonNull Transaction oldItem,
                                               @NonNull Transaction newItem) {
                    return sameItem(oldItem, newItem);
                }

                @Override
                public boolean areContentsTheSame(@NonNull Transaction oldItem,
                                                  @NonNull Transaction newItem) {
                    return sameContent(oldItem, newItem);
                }
            };

    static boolean sameItem(Transaction first, Transaction second) {
        if (first.getRemoteId() != null || second.getRemoteId() != null)
            return Objects.equals(first.getRemoteId(), second.getRemoteId());
        return first.getId() == second.getId();
    }

    static boolean sameContent(Transaction first, Transaction second) {
        return first.getAmount() == second.getAmount()
                && first.getDate() == second.getDate()
                && first.getType() == second.getType()
                && Objects.equals(first.getNote(), second.getNote())
                && Objects.equals(first.getCategoryId(), second.getCategoryId())
                && Objects.equals(first.getRemoteCategoryName(), second.getRemoteCategoryName())
                && Objects.equals(first.getRemoteCategoryColor(), second.getRemoteCategoryColor())
                && Objects.equals(first.getRemoteCategoryIcon(), second.getRemoteCategoryIcon())
                && Objects.equals(first.getRemoteReceiptId(), second.getRemoteReceiptId());
    }
}
