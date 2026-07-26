package com.example.appquanlychitieu.ui.goals;

import android.content.Context;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ImageButton;
import android.widget.PopupMenu;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.DiffUtil;
import androidx.recyclerview.widget.ListAdapter;
import androidx.recyclerview.widget.RecyclerView;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.model.Goal;
import com.example.appquanlychitieu.util.CurrencyFormatter;
import com.google.android.material.progressindicator.LinearProgressIndicator;

import java.util.ArrayList;
import java.util.Objects;

public class GoalListAdapter extends ListAdapter<Goal, GoalListAdapter.ViewHolder> {
    public interface OnGoalInteractionListener {
        void onGoalClick(Goal goal);
        void onAddFundsClick(Goal goal);
        void onEditGoalClick(Goal goal);
        void onGoalLongClick(Goal goal);
    }

    private final Context context;
    private final OnGoalInteractionListener listener;

    public GoalListAdapter(Context context, OnGoalInteractionListener listener) {
        super(DIFF_CALLBACK);
        this.context = context;
        this.listener = listener;
    }

    public void setGoals(java.util.List<Goal> goals) {
        submitList(goals == null ? new ArrayList<>() : new ArrayList<>(goals));
    }

    @NonNull
    @Override
    public ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        return new ViewHolder(LayoutInflater.from(parent.getContext())
                .inflate(R.layout.item_goal, parent, false));
    }

    @Override
    public void onBindViewHolder(@NonNull ViewHolder holder, int position) {
        holder.bind(getItem(position));
    }

    final class ViewHolder extends RecyclerView.ViewHolder {
        final TextView name, current, target, percent, completed;
        final LinearProgressIndicator progress;
        final ImageButton addFunds, more;

        ViewHolder(View view) {
            super(view);
            name = view.findViewById(R.id.tv_goal_name);
            current = view.findViewById(R.id.tv_current_amount);
            target = view.findViewById(R.id.tv_target_amount);
            percent = view.findViewById(R.id.tv_percentage);
            completed = view.findViewById(R.id.tv_goal_completed);
            progress = view.findViewById(R.id.pb_goal_progress);
            addFunds = view.findViewById(R.id.btn_add_funds);
            more = view.findViewById(R.id.btn_more);
        }

        void bind(Goal goal) {
            int value = goal.getTargetAmount() <= 0 ? 0 : (int) Math.min(100,
                    Math.round(goal.getCurrentAmount() * 100d / goal.getTargetAmount()));
            name.setText(goal.getName());
            current.setText(CurrencyFormatter.format(goal.getCurrentAmount()));
            target.setText(context.getString(R.string.budget_limit,
                    CurrencyFormatter.format(goal.getTargetAmount())));
            percent.setText(value + "%");
            progress.setProgressCompat(value, false);
            completed.setVisibility(value >= 100 ? View.VISIBLE : View.GONE);
            addFunds.setVisibility(value >= 100 ? View.GONE : View.VISIBLE);
            itemView.setOnClickListener(v -> listener.onGoalClick(goal));
            addFunds.setOnClickListener(v -> listener.onAddFundsClick(goal));
            more.setOnClickListener(v -> {
                PopupMenu menu = new PopupMenu(context, v);
                menu.getMenu().add(R.string.edit).setOnMenuItemClickListener(item -> {
                    listener.onEditGoalClick(goal);
                    return true;
                });
                menu.getMenu().add(R.string.delete);
                menu.setOnMenuItemClickListener(item -> {
                    if (item.getTitle().equals(context.getString(R.string.delete)))
                        listener.onGoalLongClick(goal);
                    return true;
                });
                menu.show();
            });
        }
    }

    private static final DiffUtil.ItemCallback<Goal> DIFF_CALLBACK =
            new DiffUtil.ItemCallback<Goal>() {
                @Override public boolean areItemsTheSame(@NonNull Goal oldItem, @NonNull Goal newItem) {
                    return sameItem(oldItem, newItem);
                }
                @Override public boolean areContentsTheSame(@NonNull Goal oldItem, @NonNull Goal newItem) {
                    return sameContent(oldItem, newItem);
                }
            };

    static boolean sameItem(Goal first, Goal second) {
        if (first.getRemoteId() != null || second.getRemoteId() != null)
            return Objects.equals(first.getRemoteId(), second.getRemoteId());
        return first.getId() == second.getId();
    }

    static boolean sameContent(Goal first, Goal second) {
        return Objects.equals(first.getName(), second.getName())
                && first.getTargetAmount() == second.getTargetAmount()
                && first.getCurrentAmount() == second.getCurrentAmount();
    }
}
