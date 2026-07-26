package com.example.appquanlychitieu.ui.budget;

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
import com.example.appquanlychitieu.data.model.Budget;
import com.example.appquanlychitieu.ui.common.CategoryVisualResolver;
import com.example.appquanlychitieu.util.CurrencyFormatter;
import com.google.android.material.progressindicator.LinearProgressIndicator;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.Map;
import java.util.Objects;

public class BudgetListAdapter extends ListAdapter<Budget, BudgetListAdapter.ViewHolder> {
    public interface Listener {
        void onEdit(Budget budget);
        void onDelete(Budget budget);
    }
    private final Context context;
    private final Map<Long, Long> spentMap = new HashMap<>();
    private Listener listener;

    public BudgetListAdapter(Context context) {
        super(DIFF_CALLBACK);
        this.context = context;
    }

    public void setListener(Listener listener) { this.listener = listener; }
    public void setBudgets(java.util.List<Budget> budgets) {
        submitList(budgets == null ? new ArrayList<>() : new ArrayList<>(budgets));
    }
    public void setSpentMap(Map<Long, Long> spent) {
        spentMap.clear();
        if (spent != null) spentMap.putAll(spent);
        notifyDataSetChanged();
    }

    @NonNull
    @Override
    public ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        return new ViewHolder(LayoutInflater.from(parent.getContext())
                .inflate(R.layout.item_budget, parent, false));
    }

    @Override
    public void onBindViewHolder(@NonNull ViewHolder holder, int position) {
        holder.bind(getItem(position));
    }

    final class ViewHolder extends RecyclerView.ViewHolder {
        final View iconBackground;
        final ImageView icon;
        final TextView name, limit, spent, remaining, percentage, status;
        final LinearProgressIndicator progress;
        final ImageButton more;

        ViewHolder(View view) {
            super(view);
            iconBackground = view.findViewById(R.id.view_icon_bg);
            icon = view.findViewById(R.id.iv_category_icon);
            name = view.findViewById(R.id.tv_category_name);
            limit = view.findViewById(R.id.tv_budget_label);
            spent = view.findViewById(R.id.tv_spent);
            remaining = view.findViewById(R.id.tv_remaining);
            percentage = view.findViewById(R.id.tv_percentage);
            status = view.findViewById(R.id.tv_budget_status);
            progress = view.findViewById(R.id.progress_budget);
            more = view.findViewById(R.id.btn_more);
        }

        void bind(Budget budget) {
            long used = spentMap.getOrDefault(budget.getCategoryId(), 0L);
            long left = budget.getAmount() - used;
            int percent = budget.getAmount() <= 0 ? 0
                    : (int) Math.round(used * 100d / budget.getAmount());
            name.setText(budget.getRemoteCategoryName());
            limit.setText(context.getString(R.string.budget_limit,
                    CurrencyFormatter.format(budget.getAmount())));
            spent.setText(context.getString(R.string.budget_spent, CurrencyFormatter.format(used)));
            remaining.setText(context.getString(left >= 0
                    ? R.string.budget_remaining_value : R.string.budget_over_value,
                    CurrencyFormatter.format(Math.abs(left))));
            percentage.setText(percent + "%");
            progress.setProgressCompat(Math.min(100, Math.max(0, percent)), false);

            int stateColor;
            if (percent >= 100) {
                stateColor = context.getColor(R.color.expense_color);
                status.setText(R.string.budget_exceeded_label);
                status.setVisibility(View.VISIBLE);
            } else if (percent >= 80) {
                stateColor = context.getColor(R.color.warning_color);
                status.setText(R.string.budget_warning);
                status.setVisibility(View.VISIBLE);
            } else {
                stateColor = context.getColor(R.color.budget_color);
                status.setVisibility(View.GONE);
            }
            progress.setIndicatorColor(stateColor);
            percentage.setTextColor(stateColor);
            remaining.setTextColor(left >= 0
                    ? context.getColor(R.color.text_secondary) : context.getColor(R.color.expense_color));

            CategoryVisualResolver.CategoryVisual visual = CategoryVisualResolver.resolve(context,
                    budget.getRemoteCategoryId(), budget.getRemoteCategoryColor());
            GradientDrawable bg = new GradientDrawable();
            bg.setShape(GradientDrawable.OVAL);
            bg.setColor(visual.baseColor);
            iconBackground.setBackground(bg);
            icon.setColorFilter(visual.onBaseColor);
            int iconRes = context.getResources().getIdentifier(
                    budget.getRemoteCategoryIcon(), "drawable", context.getPackageName());
            icon.setImageResource(iconRes == 0 ? R.drawable.ic_other : iconRes);
            more.setOnClickListener(v -> {
                PopupMenu menu = new PopupMenu(context, v);
                menu.getMenu().add(R.string.edit).setOnMenuItemClickListener(item -> {
                    if (listener != null) listener.onEdit(budget);
                    return true;
                });
                menu.getMenu().add(R.string.delete);
                menu.setOnMenuItemClickListener(item -> {
                    if (item.getTitle().equals(context.getString(R.string.delete)) && listener != null)
                        listener.onDelete(budget);
                    return true;
                });
                menu.show();
            });
        }
    }

    private static final DiffUtil.ItemCallback<Budget> DIFF_CALLBACK =
            new DiffUtil.ItemCallback<Budget>() {
                @Override public boolean areItemsTheSame(@NonNull Budget oldItem, @NonNull Budget newItem) {
                    return sameItem(oldItem, newItem);
                }
                @Override public boolean areContentsTheSame(@NonNull Budget oldItem, @NonNull Budget newItem) {
                    return sameContent(oldItem, newItem);
                }
            };

    static boolean sameItem(Budget first, Budget second) {
        if (first.getRemoteId() != null || second.getRemoteId() != null)
            return Objects.equals(first.getRemoteId(), second.getRemoteId());
        return first.getCategoryId() == second.getCategoryId()
                && Objects.equals(first.getMonthYear(), second.getMonthYear());
    }

    static boolean sameContent(Budget first, Budget second) {
        return first.getAmount() == second.getAmount()
                && Objects.equals(first.getMonthYear(), second.getMonthYear())
                && Objects.equals(first.getRemoteCategoryName(), second.getRemoteCategoryName())
                && Objects.equals(first.getRemoteCategoryColor(), second.getRemoteCategoryColor())
                && Objects.equals(first.getRemoteCategoryIcon(), second.getRemoteCategoryIcon());
    }
}
