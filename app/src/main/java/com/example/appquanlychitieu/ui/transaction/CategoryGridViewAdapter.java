package com.example.appquanlychitieu.ui.transaction;

import android.content.Context;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.ImageView;
import android.widget.TextView;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.model.Category;
import com.example.appquanlychitieu.ui.common.CategoryVisualResolver;
import com.google.android.material.card.MaterialCardView;

import java.util.ArrayList;
import java.util.List;

public class CategoryGridViewAdapter extends BaseAdapter {
    private final Context context;
    private List<Category> categories = new ArrayList<>();
    private int selectedPosition = -1;

    public CategoryGridViewAdapter(Context context) { this.context = context; }

    public void setCategories(List<Category> categories) {
        this.categories = categories == null ? new ArrayList<>() : new ArrayList<>(categories);
        selectedPosition = -1;
        notifyDataSetChanged();
    }

    public void setSelectedPosition(int position) {
        selectedPosition = position;
        notifyDataSetChanged();
    }

    public void setSelectedCategoryId(long id) {
        selectedPosition = -1;
        for (int i = 0; i < categories.size(); i++) {
            if (categories.get(i).getId() == id) { selectedPosition = i; break; }
        }
        notifyDataSetChanged();
    }

    @Override public int getCount() { return categories.size(); }
    @Override public Category getItem(int position) { return categories.get(position); }
    @Override public long getItemId(int position) { return getItem(position).getId(); }

    @Override
    public View getView(int position, View convertView, ViewGroup parent) {
        ViewHolder holder;
        if (convertView == null) {
            convertView = LayoutInflater.from(context).inflate(R.layout.item_category_grid, parent, false);
            holder = new ViewHolder(convertView);
            convertView.setTag(holder);
        } else holder = (ViewHolder) convertView.getTag();

        Category category = getItem(position);
        CategoryVisualResolver.CategoryVisual visual = CategoryVisualResolver.resolve(
                context, String.valueOf(category.getId()), category.getColor());
        holder.name.setText(category.getName());
        boolean selected = position == selectedPosition;
        holder.card.setCardBackgroundColor(selected
                ? context.getColor(R.color.primary_container) : context.getColor(R.color.surface));
        holder.card.setStrokeColor(selected
                ? context.getColor(R.color.primary) : context.getColor(R.color.outline));
        holder.card.setStrokeWidth(context.getResources().getDimensionPixelSize(
                selected ? R.dimen.stroke_width : R.dimen.stroke_width));
        holder.name.setTextColor(context.getColor(selected ? R.color.primary : R.color.text_primary));
        CategoryVisualResolver.bindIcon(holder.icon, category.getIcon(), visual.baseColor);
        return convertView;
    }

    static final class ViewHolder {
        final MaterialCardView card;
        final ImageView icon;
        final TextView name;
        ViewHolder(View view) {
            card = view.findViewById(R.id.card_category);
            icon = view.findViewById(R.id.iv_category_icon);
            name = view.findViewById(R.id.tv_name);
        }
    }
}
